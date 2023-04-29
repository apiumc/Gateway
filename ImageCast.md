
<h1 align="center" style="margin: 30px 0 30px; font-weight: bold;">Apiumc Gateway</h1>
<h4 align="center" style="margin: 30px 0 30px; font-weight: bold;">图片处理组件</h4>

<p align="center">
<a href='https://gitee.com/apiumc/Gateway/stargazers'><img src='https://gitee.com/apiumc/Gateway/badge/star.svg?theme=dark' alt='star'></img></a>
<a href="https://gitee.com/apiumc/Gateway/LICENSE"><img src="https://img.shields.io/badge/license-Apache--2.0-green"></a>
<a href="https://gitee.com/apiumc/Gateway/stargazers"><img src="https://img.shields.io/badge/version-v1.0.0-blue"></a>
</p>

### 图片处理。
图片是Web应用打开快慢关键因子，因此合适尺寸和格式能带来极大的速度提升，Apiumc网关的图片处理，在不改变原应用的情况下，来调整图片尺寸、增加水印、格式转码等等功能，同时也支持智能适配按浏览器兼容性从avif格式、webp格式、png格式转码，从而帮助应用把图片大小减少60%-90%，让应用快如闪电，大幅改善原应用的交互质量。

Apiumc配置图片处理方式有分为两种方式快捷参数和图片模板，同时查看图片参数处理后的效果有以下方式

1. 可以通过在图片的url的QueryString上追加`umc-image=xxx`，其中xxx可以是图片处理的快捷参数或图片模板。
2. 可以通过在图片的url路径上的头部加上`/UMC.Image/xxx/`等同上面效果。

**注意**：图片处理后会持久化保存并缓存，可手动在图片url后面追加`&umc=src`或?`umc=src`来获取新版本

### 快捷参数
快捷参数由三部分组成
>[`方位`][`尺寸`][`格式`]
#### 方位参数

|参数|样例|说明|
|--|--|--|
|w |w100 |表示按宽度缩小 |
|h |w100 |表示按高度缩小 |
|c |c100 |全图居中缩放指定尺寸 |
|t |t100 |向上或向左裁剪指定尺寸 |
|m |m100 |正中裁剪指定尺寸 |
|b |b100 |向下和向右裁剪指定尺寸 |


#### 尺寸参数
分为单值、-对值和x对值

|参数|样例|说明|
|--|--|--|
|单值 |100 |表示按固定正方式缩小 ，样例表示宽高各100 |
|-对值 |100-200 |表示限定尺寸，样例表示宽限定100高限定200 单边超过，则单边缩放，等同w、h方位参数，双边比例超过，则按固定大小进行裁剪 |
|x对值 |100x200 |表示按固定尺寸缩小，样例：宽100、高200 |


#### 图片转码
支持对gif、jpeg、webp、png、avif格式的相互转换，默认值为原图格式

|参数|样例|说明|
|--|--|--|
|g |w200g |转码为gif图片，样例：表示 宽度缩放到200并转化为gif格式图片 |
|j |w200j |转码为jpeg图片，样例：表示 宽度缩放到200并转化为jpeg格式图片 |
|w |w200w |转码为webp图片，样例：表示 宽度缩放到200并转化为webp格式图片 |
|p |w200p |转码为png图片，样例：表示 宽度缩放到200并转化为png格式图片 |
|a |w200a |转码为avif图片，样例：表示 宽度缩放到200并转化为avif格式图片 |
|o |w200o |适配图片格式，样例：表示 宽度缩放到200并根据浏览器从avif--webp--png来适配格式 |

注意：avif图片浏览器支持有限，webp图片主流浏览器都支持，但要考虑国产各不一样的浏览器，建议图片优化采用了适配格式。
### 图片模板
在云桌面-应用设置-托管应用，点击应用，打开应用配置中再点击图片处理如下图：

![图片](https://www.apiumc.com/UserResources/1flmgtt/1666787012/image.png!m400)

点击下出现如下图：

![图片](https://www.apiumc.com/UserResources/1flmgtt/1666785404/image.png!m400)

再此配置路径格式，支持用一个`*`进行前后对比，也支持图片Content-Type类型配置，当图片的请求路径或Conent-tye能配对图片模板，则采用模板参数来处理图片。

**注意**：在此配置的路径也可以通过在url的QueryString上加`umc-image=[模板名]`来使用，快速查看效果

再点击配置的路径，则会出现图片模板配置如下图：

![图片](https://www.apiumc.com/UserResources/1flmgtt/1666792125/image.png!m400)
#### 图片宽度
设置图片的宽度，正值为固定宽度，负值为限定宽度
#### 图片高度
设置图片的高度，正值为固定高度，负值为限定高度
#### 裁剪方式
对图片进行裁剪的方式

居中缩放：是把图片放入新尺寸中间，短边居中的效果，是缩小效果

向上裁剪：对图片长图进行向上裁剪，对图片的宽图进行向左裁剪。

居中裁剪：对图片正中进行裁剪。

向下裁剪：对图片长图进行向下裁剪，对图片的宽图进行向右裁剪。

#### 图片格式
可设置为原图格式、gif、png、jpeg、webp和适配格式

**注意**：只设置的单边值，裁剪方式设置无效。
#### 水印方式
方式水印可分可设置图片水印和文本水印

图片水印设置如下图：

![图片](https://www.apiumc.com/UserResources/1flmgtt/1666836877/image.png!m400)

**提示**：水印图片，会自动设置50%透明度复加到了图片上

文本水印设置如下图： 

![图片](https://www.apiumc.com/UserResources/1flmgtt/1666837029/image.png!m400)

**提示**：文本颜色的支持8位和4位颜色的透明度设置