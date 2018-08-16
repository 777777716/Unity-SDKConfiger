# 关于Unity版本
目前基于Unity 2018.2.0f2，C# .Net 4.x Equivalent

理论上可以支持Unity 5.x

有其他的支持需求，可以提issue或者pr

# Unity-SDKConfiger

在发布xCode/AndroidStudio工程时，根据配置文件自动化配置工程

目前只实现了在发布xCode工程的自动配置

AndroidStudio工程后续一段时间会完成

# 使用方法
## iOS xCode工程

请按照 Assets/Plugins/Editor/SDKConfiger/Demo.SDKConfiger.iOS.json 的格式来配置

保证文件后缀为 *.SDKConfiger.iOS.json

"flag":false  ：表示这个配置文件是否启用

目前支持

zipPath：相对配置文件的路径，*.zip  该文件解压后的所有文件都会被关联到xCode工程中

enableBitCode：bitCode开关

linkFrameworks：*.dylib *.framework

linkFlags：编译标识

urlSchemes：类似 微信AppID 等配置

whitelists：白名单

permissions：权限需求

nativeCodes：原生代码中插入自己的代码

# 示例
## Bugly SDK

路径：Assets/Plugins/BuglyPlugins

配置文件：Assets/Plugins/BuglyPlugins/iOS/Bugly.SDKConfiger.iOS.json
