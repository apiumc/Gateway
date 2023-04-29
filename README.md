<h1 align="center" style="margin: 30px 0 30px; font-weight: bold;">Apiumc Gateway</h1>
<h4 align="center" style="margin: 10px 0 10px; font-weight: bold;">它一个工具等于 Nginx + Https证书 + 内网穿透 + 图片切割水印 + 网关登录</h4>

<p align="center">
<a href='https://gitee.com/apiumc/Gateway/stargazers'><img src='https://gitee.com/apiumc/Gateway/badge/star.svg?theme=dark' alt='star'></img></a>
<a href="https://gitee.com/apiumc/Gateway/LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-green"></a>
<a href="https://gitee.com/apiumc/Gateway/stargazers"><img src="https://img.shields.io/badge/version-v1.0.0-blue"></a>

### 介绍说明

Apiumc Gateway 是高性能的Web网关，它从底层Socket原始通信层开始，采用多线程、多任务模式从新构建Web服务，充分发挥当下多核的CPU的多任务并行性能，达到不输nginx的性能表现，而多线程、多任务天生比多进程模式更有编程可控性，基于这此原理，为Apiumc带来丰富多的基于网关深度应用，是网关功能集大成者；它一个工具等于`Nginx` +` 网关登录` + `图片处理` + `内网穿透` + `免费Ssl证书`，且配置全程界面化，让你告别难懂、难记易出错的指令配置；

在追求功能多样性上性能也无语伦比，拥有多种措施大幅度改善源应用性能，是企业和从业者非常值得掌握的的Web应用托管工具，是F5国产替代首先。



### 下载安装

1. 从发行版处或官网上下载对应操作系统下的版本，解压运行即可。
![image](https://www.apiumc.com/UserResources/7124914603020058625/1682142694/image.png)
2. 在浏览器中输入监听地址中的网址，用管理员进行登录， 按提示完成注册登记，默认管理员为admin，密码也是admin。
![image](https://www.apiumc.com/UserResources/7124914603020058625/1682142739/image.png)
 

### Https证书
Apiumc内置了Https证书管理，因DV类型域名证书可以通过文件验证来签发证书，只要域名解释到Apiumc就自然能通过文件验证，利用此特性，Apiumc团队与知名证书机构达成合作，为各位免费签发DV域名证书，为建设更安全的网络环境，让网络更安全贡献自己的一份力量。

注册后，可以免费申请Https证书，两种方式如下。
1.  在Apiumc指令窗口 输入 `ssl [domain]`，如下图:
![image](https://www.apiumc.com/UserResources/7124914603020058625/1682583887/image.png)
2.  在`云桌面`--`应用设置`--`网关服务`中申请，如下图:
![image](https://www.apiumc.com/UserResources/7124914603020058625/1682584153/image.png)

Apiumc不但可以免费签发域名证书，也支持过期自动签发新证书、并自动部署证书，帮助各运维从复杂证书部署更新解放出来。

### 内网穿透
Apiumc内置内网穿透支持，Apiumc是Web的反向代理，只要把外网服务器的请求通过Host域名来区分进行点对点的转发到本地Apiumc，对Apiumc来说转发的请求数据和平常网络防问没有区别，再把响应的数据以点对点的转发外网服务器，完成Http协议的内网穿透，这样外网就可通过Web形式防问本机或内网应用。

注册后，也可以开启Web VPN（内网穿透），开启方式两种:
1.  在Apiumc指令窗口，输入 `vpn start`，如下图:
![image](https://www.apiumc.com/UserResources/7124914603020058625/1682585479/image.png)
2.  在`云桌面`--`应用设置`的Web VPN中状态栏，点击则可启动Web VPN了，如下图：
![image](https://www.apiumc.com/UserResources/7124914603020058625/1682584037/image.png)

开启后，会分配一个永久不变的域名，用此域名防问将会联通本机的Apiumc了，内网穿透支持绑定域名，只要域名用CNAME解释到分配永久不变的域名，就完成了绑定，从而让内部应用被外网用Web浏览器防问了。[了解更多](WebVPN.md) ...


### 图片切割

Apiumc内置图片切割水印，原理是通过代理响应后，根据参数转化图片，并缓存，所以在不改变原应用的情况下做到来调整图片尺寸、添加水印、格式转码等等功能，支持根据浏览器从avif格式、webp格式、png格式智能适配，从而让图片网络流量减少60%-90%，节省大量流量费用，还让应用快如闪电，大幅改善原应用的交互质量。[了解更多](ImageCast.md) ...



### 网关登录

网关登录是相比单点登录形式来说，它无需改造第三方应用，帮助企业各应用快速实现统一登录。与应用身份对接是通过网关技术来兼容企业现有应用，让各应用身份对接在线配置即可，配置过程中原应用无感知，对企业来说协调各应用负责人更容易，整体拥有成本更低。


相对于Https证书、内网穿透、图片切割是从网关出发对具体事务创新性实现，而企业的统一登录是企业身份体系和各应用的梳理和诊断，并根据Apiumc提供的7种登录方式提练出Api，配置身份配置转化，是一个专业性实施性解决方案，相对来说我们开拓的网关登录技术路线比传统经典单点登录更有优势，因为网关登录方案不用改造第三方应用，少了各应用适配登录协议的二次开发工具，还有节省更多的是企业协调成本更，还想更进一步了解网关登录，欢迎咨询我们，乐意与各位分享我们在各企业实施统一登录的研究成果。

### 加入我们

Apiumc是用网关形式来加强应用，这是一个新的场景，目前我们用.net core完成了核心部分，性能也相当不错；还有很多场景需要专业人员加入，我们才能丰富，例如日志分析，虽然Apiumc已经能很方便的能收集日志，也能收集其它收集需要数据埋点才能收集的用户维度，但目前来说，我们只是按身份收集全日志，从分析层面来讲，还还远远不够，如何去丰富这个带用户维度日志模型，需要各位加入一起加入完善；

Apiumc有免费的Https证书和内网穿透，不要忘记给你的 ⭐️ Star ⭐️哦，你们关注是我们推出免费服务动力。






