using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine;

namespace SDKConfiger
{
    public static class Utils
    {
        public static bool Unzip( string zipPath, string unzipDir, out string unzipPath )
        {
            using( var zis = new ZipInputStream( File.OpenRead( zipPath ) ) )
            {
                var tmpDir = Path.Combine( Path.GetTempPath(), Path.GetFileNameWithoutExtension( zipPath ) );
                ZipEntry theEntry;
                while( (theEntry = zis.GetNextEntry()) != null )
                {
                    if( !theEntry.Name.Contains( "__MACOSX" ) && !theEntry.Name.EndsWith( ".DS_Store" ) )
                    {
                        if( !string.IsNullOrEmpty( Path.GetFileName( theEntry.Name ) ) )
                        {
                            var filePath = Path.Combine( tmpDir, theEntry.Name );
                            var dirPath = Path.GetDirectoryName( filePath );
                            if( !Directory.Exists( dirPath ) )
                            {
                                Directory.CreateDirectory( dirPath );
                            }

                            using( var fs = File.Create( filePath ) )
                            {
                                var size = 2048;
                                var buffer = new byte[size];
                                while( (size = zis.Read( buffer, 0, buffer.Length )) > 0 )
                                {
                                    fs.Write( buffer, 0, size );
                                }
                            }
                        }
                    }
                }

                if( Directory.Exists( tmpDir ) )
                {
                    var files = Directory.GetFiles( tmpDir );
                    var dirs = Directory.GetDirectories( tmpDir );
                    if( files.Length + dirs.Length > 0 )
                    {
                        if( files.Length == 0 && dirs.Length == 1 )
                        {
                            unzipPath = Path.Combine( unzipDir, Path.GetFileName( dirs[0] ) );
                            if( Directory.Exists( unzipPath ) )
                            {
                                Directory.Delete( unzipPath, true );
                            }

                            Directory.Move( dirs[0], unzipPath );
                            Directory.Delete( tmpDir );
                        }
                        else
                        {
                            unzipPath = Path.Combine( unzipDir, Path.GetFileNameWithoutExtension( zipPath ) );
                            if( Directory.Exists( unzipPath ) )
                            {
                                Directory.Delete( unzipPath, true );
                            }

                            Directory.Move( tmpDir, unzipPath );
                        }

                        return true;
                    }
                }

                unzipPath = null;
                return false;
            }
        }


        public static List<string> GetAllFiles( string dirPath )
        {
            var paths = new List<string>();
            foreach( var item in Directory.GetFiles( dirPath ) )
            {
                if( !item.EndsWith( ".DS_Store" ) )
                {
                    paths.Add( item );
                }
            }

            foreach( var item in Directory.GetDirectories( dirPath ) )
            {
                if( item.EndsWith( ".bundle" ) || item.EndsWith( ".framework" ) )
                {
                    paths.Add( item );
                }
                else
                {
                    paths.AddRange( GetAllFiles( item ) );
                }
            }

            return paths;
        }

        public static List<string> GetAllDirsContainFileExtension( string dirPath, string extension )
        {
            var paths = new List<string>();
            if( Directory.GetFiles( dirPath ).Any( item => Path.GetExtension( item ) == extension ) )
            {
                paths.Add( dirPath );
            }

            foreach( var subDirPath in Directory.GetDirectories( dirPath ) )
            {
                var dirExtension = Path.GetExtension( subDirPath );
                if( dirExtension != ".bundle" && dirExtension != ".framework" )
                {
                    paths.AddRange( GetAllDirsContainFileExtension( dirPath, extension ) );
                }
                else if( dirExtension == extension )
                {
                    paths.Add( dirPath );
                }
            }

            return paths;
        }

        public static void Insert( string path, string existMark, string insertMark, int insertOffset, IEnumerable<string> insertCodes )
        {
            var lines = new List<string>( File.ReadAllLines( path ) );
            var exist = false;
            foreach( var line in lines )
            {
                if( line == existMark )
                {
                    exist = true;
                    break;
                }
            }

            if( !exist )
            {
                var index = 0;
                if( !string.IsNullOrEmpty( insertMark ) )
                {
                    index = lines.IndexOf( insertMark );
                    if( index == -1 )
                    {
                        Debug.LogError( $"不存在该insertMark：{insertMark}" );
                        return;
                    }

                    index += insertOffset;
                }

                lines.Insert( index++, existMark );
                foreach( var insertCode in insertCodes )
                {
                    lines.Insert( index++, insertCode );
                }

                File.WriteAllLines( path, lines.ToArray() );
            }
        }


        public static XmlElement CreateNode( XmlDocument xmlDoc, string name, string innerText )
        {
            var node = xmlDoc.CreateElement( name );
            node.InnerText = innerText;
            return node;
        }

        public static XmlElement CreateCFBundleTypeRole( XmlDocument xmlDoc, string str )
        {
            // <dict>
            var dict = xmlDoc.CreateElement( "dict" );

            // <key>CFBundleTypeRole</key>
            dict.AppendChild( CreateNode( xmlDoc, "key", "CFBundleTypeRole" ) );

            // 	<string>Editor</string>
            dict.AppendChild( CreateNode( xmlDoc, "string", "Editor" ) );

            // 	<key>CFBundleURLSchemes</key>
            dict.AppendChild( CreateNode( xmlDoc, "key", "CFBundleURLSchemes" ) );

            // 	<array>
            // 		<string>str</string>
            // 	</array>
            dict.AppendChild( xmlDoc.CreateElement( "array" ).AppendChild( CreateNode( xmlDoc, "string", str ) ) );
            return dict;
        }

        public static void ForEach( XmlNodeList nodes, Action<XmlNode> action )
        {
            if( nodes != null )
            {
                for( var i = 0; i < nodes.Count; i++ )
                {
                    action( nodes[i] );
                }
            }
        }

        public static XmlElement CreateArray( XmlDocument xmlDoc, XmlNode dict, string key )
        {
            // <key>……</key>
            dict.AppendChild( CreateNode( xmlDoc, "key", key ) );
            // <array>……</array>
            var array = xmlDoc.CreateElement( "array" );
            dict.AppendChild( array );
            return array;
        }

        public static XmlNodeList FindArray( XmlNode dict, string key )
        {
            var list = dict.ChildNodes;
            for( var i = 0; i < list.Count; i++ )
            {
                if( list[i].InnerText == key )
                {
                    return list[i + 1].ChildNodes;
                }
            }

            return null;
        }

        public static void RemoveArray( XmlNode dict, string key )
        {
            var list = dict.ChildNodes;
            for( var i = 0; i < list.Count; i++ )
            {
                if( list[i].InnerText == key )
                {
                    // <key>key</key>
                    dict.RemoveChild( list[i] );
                    // <array>……</array>
                    dict.RemoveChild( list[i] );
                }
            }
        }
    }
}