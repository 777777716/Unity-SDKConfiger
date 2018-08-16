using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using SDKConfiger.MiniJSON;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace SDKConfiger
{
    public class NativeCodeInsertVO
    {
        public string targetFile;
        public string existMark;
        public string insertMark;
        public int insertOffset;
        public List<string> insertCodes = new List<string>();

        private int hashCode = 0;

        public override int GetHashCode()
        {
            if( hashCode == 0 )
            {
                var sb = new StringBuilder();
                sb.Append( targetFile );
                sb.Append( existMark );
                sb.Append( insertMark );
                sb.Append( insertOffset );
                foreach( var insertCode in insertCodes )
                {
                    sb.Append( insertCode );
                }

                hashCode = sb.ToString().GetHashCode();
            }

            return hashCode;
        }
    }

    public static class SDKConfiger
    {
        [PostProcessBuild( 0 )]
        public static void Config( BuildTarget target, string exportPath )
        {
#if UNITY_IOS
            if( target == BuildTarget.iOS )
            {
                ConfigIOS( exportPath );
            }
#endif
        }

#if UNITY_IOS
        private static void ConfigIOS( string exportPath )
        {
            var zipPaths = new HashSet<string>();
            var enableBitCode = true;
            var linkFrameworks = new HashSet<string>();
            var linkFlags = new HashSet<string>();
            var urlSchemes = new HashSet<string>();
            var whitelists = new HashSet<string>();
            var permissions = new Dictionary<string, string>();
            var nativeCodes = new HashSet<NativeCodeInsertVO>();
            foreach( var filePath in Directory.GetFiles( Application.dataPath, "*.SDKConfiger.iOS.json", SearchOption.AllDirectories ) )
            {
                var json = File.ReadAllText( filePath );
                var config = Json.Deserialize( json ) as Dictionary<string, object>;
                if( !(bool)config["flag"] )
                {
                    continue;
                }

                var zipPath = config["zipPath"].ToString();
                if( !Path.IsPathRooted( zipPath ) )
                {
                    zipPath = Path.GetFullPath( Path.Combine( Path.GetDirectoryName( filePath ), zipPath ) );
                }

                zipPaths.Add( zipPath );
                if( enableBitCode && !(bool)config["enableBitCode"] )
                {
                    enableBitCode = false;
                }

                foreach( var item in config["linkFrameworks"] as List<object> )
                {
                    linkFrameworks.Add( item.ToString() );
                }

                foreach( var item in config["linkFlags"] as List<object> )
                {
                    linkFlags.Add( item.ToString() );
                }

                foreach( var item in config["urlSchemes"] as List<object> )
                {
                    urlSchemes.Add( item.ToString() );
                }

                foreach( var item in config["whitelists"] as List<object> )
                {
                    whitelists.Add( item.ToString() );
                }

                foreach( var item in config["permissions"] as List<object> )
                {
                    var map = item as Dictionary<string, object>;
                    permissions.Add( map["permission"].ToString(), map["describe"].ToString() );
                }

                foreach( var item in config["nativeCodes"] as List<object> )
                {
                    var map = item as Dictionary<string, object>;
                    var vo = new NativeCodeInsertVO
                    {
                        targetFile = map["targetFile"].ToString(),
                        existMark = map["existMark"].ToString(),
                        insertMark = map["insertMark"].ToString(),
                        insertOffset = (int)map["insertOffset"]
                    };
                    foreach( var code in map["insertCodes"] as List<object> )
                    {
                        vo.insertCodes.Add( code.ToString() );
                    }

                    nativeCodes.Add( vo );
                }
            }

            ConfigIOS( exportPath, zipPaths, enableBitCode, linkFrameworks, linkFlags, urlSchemes, whitelists, permissions,
                       nativeCodes );
        }

        private static void ConfigIOS( string exportPath,
            HashSet<string> zipPaths,
            bool enableBitCode,
            HashSet<string> linkFrameworks,
            HashSet<string> linkFlags,
            HashSet<string> urlSchemes,
            HashSet<string> whitelists,
            Dictionary<string, string> permissions,
            HashSet<NativeCodeInsertVO> nativeCodes )
        {
            if( exportPath[exportPath.Length - 1] != Path.DirectorySeparatorChar )
            {
                exportPath += Path.DirectorySeparatorChar;
            }

            //读取xCode工程
            var proj = new PBXProject();
            proj.ReadFromFile( PBXProject.GetPBXProjectPath( exportPath ) );

            var targetGuid = proj.TargetGuidByName( "Unity-iPhone" );

            //添加自定义文件
            foreach( var zipPath in zipPaths )
            {
                //解压zip 
                string unzipPath;
                if( Utils.Unzip( zipPath, exportPath, out unzipPath ) )
                {
                    //关联到项目 
                    foreach( var item in Utils.GetAllFiles( unzipPath ) )
                    {
                        var path = item.Replace( exportPath, "" )
                                       .Replace( Path.DirectorySeparatorChar, '/' );
                        proj.AddFileToBuild( targetGuid, proj.AddFile( path, path ) );
                    }

                    //添加Library路径
                    foreach( var item in Utils.GetAllDirsContainFileExtension( unzipPath, ".a" ) )
                    {
                        var path = item.Replace( exportPath, "$(PROJECT_DIR)/" )
                                       .Replace( Path.DirectorySeparatorChar, '/' );
                        proj.AddBuildProperty( targetGuid, "LIBRARY_SEARCH_PATHS", path );
                    }


                    //添加Framework路径
                    foreach( var item in Utils.GetAllDirsContainFileExtension( unzipPath, ".framework" ) )
                    {
                        var path = item.Replace( exportPath, "$(PROJECT_DIR)/" )
                                       .Replace( Path.DirectorySeparatorChar, '/' );
                        proj.AddBuildProperty( targetGuid, "FRAMEWORK_SEARCH_PATHS", path );
                    }
                }
            }

            //修改 bitcode
            if( !enableBitCode )
            {
                proj.AddBuildProperty( targetGuid, "ENABLE_BITCODE", "NO" );
            }

            //添加Link Binary
            foreach( var name in linkFrameworks )
            {
                if( name.EndsWith( ".framework" ) )
                {
                    proj.AddFrameworkToProject( targetGuid, name, false );
                }
                else if( name.EndsWith( ".tbd" ) || name.EndsWith( ".dylib" ) )
                {
                    var path = name.Replace( ".dylib", ".tbd" );
                    var projPath = "Frameworks/" + name.Replace( ".dylib", ".tbd" );
                    proj.AddFileToBuild( targetGuid, proj.AddFile( path, projPath, PBXSourceTree.Sdk ) );
                }
            }

            //添加Link Flags
            foreach( var linkFlag in linkFlags )
            {
                proj.AddBuildProperty( targetGuid, "OTHER_LDFLAGS", linkFlag );
            }

            //修改plist
            var plistPath = Path.Combine( exportPath, "Info.plist" );
            var xmlDoc = new XmlDocument();
            xmlDoc.Load( plistPath );
            // <plist version="1.0">
            //   <dict>……</dict>
            // </plist>
            var dict = xmlDoc.DocumentElement.FirstChild;

            //添加URLSchemes
            Utils.ForEach( Utils.FindArray( dict, "CFBundleURLTypes" ), ( node ) =>
            {
                urlSchemes.Add( node["array"].FirstChild.InnerText );
            } );
            Utils.RemoveArray( dict, "CFBundleURLTypes" );

            var urlSchemeArray = Utils.CreateArray( xmlDoc, dict, "CFBundleURLTypes" );

            foreach( var urlScheme in urlSchemes )
            {
                urlSchemeArray.AppendChild( Utils.CreateCFBundleTypeRole( xmlDoc, urlScheme ) );
            }

            //添加白名单
            Utils.ForEach( Utils.FindArray( dict, "LSApplicationQueriesSchemes" ), ( node ) =>
            {
                whitelists.Add( node.InnerText );
            } );
            Utils.RemoveArray( dict, "LSApplicationQueriesSchemes" );

            var querySchemeArray = Utils.CreateArray( xmlDoc, dict, "LSApplicationQueriesSchemes" );

            foreach( var item in whitelists )
            {
                querySchemeArray.AppendChild( Utils.CreateNode( xmlDoc, "string", item ) );
            }

            //添加权限
            Utils.ForEach( dict.ChildNodes, ( node ) =>
            {
                if( permissions.ContainsKey( node.InnerText ) )
                {
                    permissions.Remove( node.InnerText );
                }
            } );

            foreach( var permission in permissions )
            {
                dict.AppendChild( Utils.CreateNode( xmlDoc, "key", permission.Key ) );
                dict.AppendChild( Utils.CreateNode( xmlDoc, "string", permission.Value ) );
            }

            xmlDoc.Save( plistPath );

            //TODO 好像是UNITY的问题，很莫名其妙
            //<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd"[]>
            //to
            //<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            File.WriteAllText( plistPath, File.ReadAllText( plistPath ).Replace( "[]>", ">" ) );

            //保存xCode工程
            File.WriteAllText( PBXProject.GetPBXProjectPath( exportPath ), proj.WriteToString() );

            //插入原生代码
            foreach( var vo in nativeCodes )
            {
                Utils.Insert( Path.Combine( exportPath, vo.targetFile ), vo.existMark, vo.insertMark, vo.insertOffset, vo.insertCodes );
            }
        }
#endif
    }
}