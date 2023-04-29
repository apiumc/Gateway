using System;
using System.Collections.Generic;
using UMC.Web;
using UMC.Data.Entities;

namespace UMC.Proxy.Activities
{
    /// <summary>
    /// 图片处理配置
    /// </summary>
    [UMC.Web.Mapping("Proxy", "ConfImage", Auth = WebAuthType.User)]
    public class SiteConfImageActivity : WebActivity
    {
        string GetPostion(String m)
        {
            switch (m ?? "0")
            {
                default:
                case "0":
                    return "上左";
                case "1":
                    return "上中";
                case "2":
                    return "上右";
                case "3":
                    return "中左";
                case "4":
                    return "居中";
                case "5":
                    return "中右";
                case "6":
                    return "下左";
                case "7":
                    return "下中";
                case "8":
                    return "下右";
            }
        }
        string Prex(string l)
        {
            var v = Utility.IntParse(l, 0);
            if (v == 0)
            {
                return "自适应";
            }
            else if (v > 0)
            {
                return $"{v}px";
            }
            else
            {
                return $"限定{v}px";

            }
        }
        public override void ProcessActivity(WebRequest request, WebResponse response)
        {
            var mainKey = this.AsyncDialog("Key", g =>
            {
                this.Prompt("请传入KEY");
                return this.DialogValue("none");
            });
            var config = UMC.Data.DataFactory.Instance().Config(mainKey) ?? new Config();
            var confValue = UMC.Data.JSON.Deserialize<WebMeta>(config.ConfValue) ?? new WebMeta();
            var key = this.AsyncDialog("ConfValue", g =>
            {
                var ms = request.SendValues ?? request.Arguments;
                if (ms.ContainsKey("limit") == false)
                {
                    this.Context.Send(new UISectionBuilder(request.Model, request.Command, request.Arguments)
                        .RefreshEvent($"{request.Model}.{request.Command}")
                        .Builder(), true);
                }
                var title = UITitle.Create();

                title.Title = "图片模板";


                var ui = UISection.Create(title);
                //ConfValue[]
                ui.AddCell("图片宽度", Prex(confValue.Get("Width")), new UIClick(new WebMeta(request.Arguments).Put(g, "Width")).Send(request.Model, request.Command));
                ui.AddCell("图片高度", Prex(confValue.Get("Hight")), new UIClick(new WebMeta(request.Arguments).Put(g, "Hight")).Send(request.Model, request.Command));
                var lui = ui.NewSection();
                var model = confValue.Get("Model") ?? "0";
                switch (model)
                {
                    default:
                    case "0":
                        lui.AddCell("裁剪方式", "居中缩放", new UIClick(new WebMeta(request.Arguments).Put(g, "Model")).Send(request.Model, request.Command));
                        break;
                    case "1":
                        lui.AddCell("裁剪方式", "向上裁剪", new UIClick(new WebMeta(request.Arguments).Put(g, "Model")).Send(request.Model, request.Command));
                        break;
                    case "2":
                        lui.AddCell("裁剪方式", "居中裁剪", new UIClick(new WebMeta(request.Arguments).Put(g, "Model")).Send(request.Model, request.Command));
                        break;
                    case "3":
                        lui.AddCell("裁剪方式", "向下裁剪", new UIClick(new WebMeta(request.Arguments).Put(g, "Model")).Send(request.Model, request.Command));
                        break;
                }
                var format3 = confValue["Format"] ?? "Src";
                switch (format3)
                {
                    case "Src":
                        format3 = "原图格式";
                        break;
                    case "Optimal":
                        format3 = "智能格式";
                        break;
                }

                lui = ui.NewSection().AddCell("图片格式", format3, new UIClick(new WebMeta(request.Arguments).Put(g, "Format")).Send(request.Model, request.Command));
                lui = ui.NewSection();
                //lui.Header.Put("text","")
                var wateRmark = confValue.Get("WateRmark") ?? "None";
                switch (wateRmark)
                {
                    case "None":
                        lui.AddCell("水印方式", "不启用", new UIClick(new WebMeta(request.Arguments).Put(g, "WateRmark")).Send(request.Model, request.Command));
                        break;
                    case "Image":
                        lui.AddCell("水印方式", "图片水印", new UIClick(new WebMeta(request.Arguments).Put(g, "WateRmark")).Send(request.Model, request.Command))
                        .NewSection().AddCell("水印方位", GetPostion(confValue["Postion"]), new UIClick(new WebMeta(request.Arguments).Put(g, "Postion")).Send(request.Model, request.Command))
                        .AddCell("水印图片", String.IsNullOrEmpty(confValue["ImagePath"]) ? "未设置" : "已设置", new UIClick(new WebMeta(request.Arguments).Put(g, "ImagePath")).Send(request.Model, request.Command))
                        .AddCell("水印占比", $"{confValue["ImageSize"] ?? "5"}%", new UIClick(new WebMeta(request.Arguments).Put(g, "ImageSize")).Send(request.Model, request.Command))
                        .AddCell("离边距离", $"{confValue["Padding"] ?? "10"}px", new UIClick(new WebMeta(request.Arguments).Put(g, "Padding")).Send(request.Model, request.Command));
                        break;
                    case "Text":
                        lui.AddCell("水印方式", "文本水印", new UIClick(new WebMeta(request.Arguments).Put(g, "WateRmark")).Send(request.Model, request.Command))
                        .NewSection().AddCell("水印方位", GetPostion(confValue["Postion"]), new UIClick(new WebMeta(request.Arguments).Put(g, "Postion")).Send(request.Model, request.Command))
                        .AddCell("水印文本", confValue.Get("WateRmarkText") ?? "未设置", new UIClick(new WebMeta(request.Arguments).Put(g, "WateRmarkText")).Send(request.Model, request.Command))
                        .AddCell("文本大小", confValue["FontSize"] ?? "12", new UIClick(new WebMeta(request.Arguments).Put(g, "FontSize")).Send(request.Model, request.Command))
                        .AddCell("文本颜色", confValue["FontColor"] ?? "#fff", new UIClick(new WebMeta(request.Arguments).Put(g, "FontColor")).Send(request.Model, request.Command))
                        .AddCell("文本字体", confValue["Font"] ?? "默认", new UIClick(new WebMeta(request.Arguments).Put(g, "Font")).Send(request.Model, request.Command))
                        .AddCell("离边距离", $"{confValue["Padding"] ?? "10"}px", new UIClick(new WebMeta(request.Arguments).Put(g, "Padding")).Send(request.Model, request.Command));
                        break;
                }
                response.Redirect(ui);

                return this.DialogValue("none");

            });
            var ConValue = UIDialog.AsyncDialog(this.Context, "Value", r =>
            {

                switch (key)
                {
                    case "Height":
                        var Height = new UIFormDialog() { Title = "高度设置" };
                        Height.AddNumber("图片高度", "Value", confValue[key]);
                        Height.AddPrompt("0为自适应，负数限制高度，正数为固定高度");
                        Height.Submit("确认", $"{request.Model}.{request.Command}");
                        return Height;
                    case "Width":
                        var Width = new UIFormDialog() { Title = "宽度设置" };
                        Width.AddNumber("图片宽度", "Value", confValue[key]);
                        Width.AddPrompt("0为自适应，负数限制宽度，正数为固定宽度");
                        Width.Submit("确认", $"{request.Model}.{request.Command}");
                        return Width;
                    case "Model":
                        var Model = new UISheetDialog() { Title = "裁剪方式" };
                        Model.Put("居中缩放", "0").Put("向上裁剪", "1").Put("居中裁剪", "2").Put("向下裁剪", "3");
                        return Model;
                    case "ImagePath":
                        this.AsyncDialog(r, "System", "Resource");
                        var ImagePath = new UIFormDialog() { Title = "水印图片" };
                        ImagePath.AddOption("图片路径", "Value", confValue[key], String.IsNullOrEmpty(confValue[key]) ? "" : "已设置")
                        .Command("System", "Resource");
                        //.Command("System", "Dir", new WebMeta().Put("filter", "*.png,*.gif,*.webp,*.jpg,*.jpeg").Put("Key","Value"));
                        ImagePath.AddPrompt("图片为静态资源路径");
                        ImagePath.Submit("确认", $"{request.Model}.{request.Command}");
                        return ImagePath;
                    case "Postion":
                        var postion = new UISheetDialog() { Title = "水印方位" };
                        postion.Cells(3);
                        postion.Put("上左", "0").Put("上中", "1").Put("上右", "2")
                        .Put("中左", "3").Put("居中", "4").Put("中右", "5")
                        .Put("下左", "6").Put("下中", "7").Put("下右", "8");
                        return postion;
                    case "Format":
                        var Format = new UISheetDialog() { Title = "图片格式" };
                        Format.Cells(3)
                        .Put("原格式", "Src").Put("Gif", "gif").Put("Jpeg", "jpeg").Put("智能格式", "Optimal").Put("Png", "png").Put("Webp", "webp");
                        return Format;
                    default:
                    case "WateRmark":
                        var wateRmark = new UISheetDialog() { Title = "水印支持" };
                        wateRmark.Put("关闭水印", "None").Put("图片水印", "Image").Put("文本水印", "Text");
                        return wateRmark;
                    case "ImageSize":
                        return new UINumberDialog(confValue["ImageSize"] ?? "5") { Title = "水印占比" };
                    case "WateRmarkText":
                        return new UITextDialog(confValue["WateRmarkText"]) { Title = "水印文本" };
                    case "Padding":
                        return new UINumberDialog(confValue["Padding"] ?? "10") { Title = "离边距离" };
                    case "FontSize":
                        return new UINumberDialog(confValue["FontSize"] ?? "12") { Title = "文本大小" };
                    case "FontColor":

                        var fontColor = new UIFormDialog() { Title = "文本颜色" };
                        fontColor.AddText("文本颜色", "Value", confValue[key] ?? "#fff");
                        //FontColor.AddPrompt("#fff");
                        fontColor.Submit("确认", $"{request.Model}.{request.Command}");
                        return fontColor;
                    case "Font":
                        var fonts = new List<WebMeta>();
                        Utility.Each(SkiaSharp.SKFontManager.Default.FontFamilies, n => fonts.Add(new WebMeta().Put("Name", n)));

                        //Utility.Each(SixLabors.Fonts.SystemFonts.Families, n => fonts.Add(new WebMeta().Put("Name", n.Name)));

                        var font = UIGridDialog.Create(new UIGridDialog.Header("Name", 0).PutField("Name", "字体"), fonts.ToArray());
                        font.Title = "文本字体";
                        return font;
                }

            });
            switch (key)
            {
                case "FontColor":
                    if (SkiaSharp.SKColor.TryParse(ConValue, out var _) == false)
                    {
                        this.Prompt("颜色值的格式不正确");
                    }
                    break;
                case "ImagePath":
                    if (System.IO.File.Exists(Data.Reflection.ConfigPath($"Static/{ConValue}")) == false)
                    {

                        this.Prompt("静态资源中并没有此文件");
                    }
                    break;
            }
            confValue.Put(key, ConValue);
            Config platformConfig = new Config();
            platformConfig.ConfKey = mainKey;
            platformConfig.ConfValue = UMC.Data.JSON.Serialize(confValue);
            UMC.Data.DataFactory.Instance().Put(platformConfig);
            this.Context.Send($"{request.Model}.{request.Command}", true);
        }
    }
}