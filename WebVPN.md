
<h1 align="center" style="margin: 30px 0 30px; font-weight: bold;">Apiumc Gateway</h1>
<h3 align="center" style="margin: 20px 0 20px; font-weight: bold;">Web VPN内网穿透组件</h4>

<p align="center">
<a href='https://gitee.com/apiumc/Gateway/stargazers'><img src='https://gitee.com/apiumc/Gateway/badge/star.svg?theme=dark' alt='star'></img></a>
<a href="https://gitee.com/apiumc/Gateway/LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-green"></a>
<a href="https://gitee.com/apiumc/Gateway/stargazers"><img src="https://img.shields.io/badge/version-v1.0.0-blue"></a>


什么是WebVPN，WebVPN是Apiumc的内网穿透组件，一种不需要服务器，就能内网资源能被外网访问的新型技术，是构建Web形式防问内部资源的VPN，相比部署传统VPN需要消耗大量人力成本，带来复杂的IT运营压力，且不稳定、易掉线、容易被打穿导致内网渗透，给企业带来一定的外部攻击风险。而WebVPN它简化了VPN网络搭建运维管理，十分钟就能让外网防问内网应用，再结合Apiumc网关安全体系，就可运营高安全、低成本、稳定、可靠用浏览器访问内网资源的的Web VPN了。

### 开启穿透

WebVPN是Apiumc网关的穿透组件，应用注册登记成功后，就可以启用WebVPN了。开启Web VPN有两种方式

1. 在线开启
在云桌面上点击“应用设置”，如下图：（需要管理员权限）<br>
![图片](https://www.apiumc.com/UserResources/1usm4ih/1644735048416/image.png!m400)<br>
如果显示未开启，则点击开启，再点击则会关停穿透，开启后，点击Web VPN网址，则就可以看到Apiumc云桌面了。

2. 命令开启
运行Apiumc指令窗口，输入指令`vpn start`，则开启内网穿透，如下图：<br>
![image](https://www.apiumc.com/UserResources/7124914603020058625/1682249241/image.png!m400)<br>
在浏览器输入Web VPN的服务网址，则就可以看到的穿透的Apiumc网关云桌面。

### 自定义域名

 只要把域名用CName解释到Web VPN的服务网址域名就可以了，就可以此域名防问穿透内容了，如下图：<br>
![image](https://www.apiumc.com/UserResources/7124914603020058625/1682237863/image.png!m400)

### 缓存配置

为了提高Web页面打开的速度，在WebVPN上也采用了动静分离的缓存机制，全局默认对.gif,.bmp,.png,.jpg,.jpeg,.ico,.webp,.svg,.css,.less,.sass,.scss,.js,.jsx,.coffee,.ts,.ttf,.woff,.woff2,.wasm页面进行缓存。

1. 在浏览器的地址栏输入路径再加上`?umc=cache`，则把此路径配对的资源进行缓存，如下图：<br>
![image](https://www.apiumc.com/UserResources/7124914603020058625/1682251583/image.png!m400) 

2. 在浏览器的地址栏输入路径再加上`?umc=cache.none`，则关闭此路径缓存机制

3. 在浏览器的地址栏输入路径再加上`?umc=cache.clear`，则清除此路径自定义缓存配置，只采用全局缓存机制

### 刷新缓存
有缓存就有可能与服务器资源版本不一致，需要一致性就需要我们来刷新缓存了，刷新缓存有两种方式

1. 删除所有缓存
方式是：在根路径上输入`/?umc`，则打开如下图：<br>
![图片](https://www.apiumc.com/UserResources/1mwlgcz/1666740721/image.png!m400)<br>
点击清空缓存，则清空域名下所有缓存。

2. 手动刷新单页缓存
方式是：在url最后追加`?umc=src`或者`&umc=src`，则可刷新此url的缓存

### 常见问题

1. 浏览器不加载新版本<br>
WebVPN有缓存机制，请参考上例改变此路径下缓存配置，选择最合适自己的。

2. 图片验证码失效<br>
需要关闭图片验证码路径下缓存机制。


