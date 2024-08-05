using Egorozh.ColorPicker;
using Egorozh.ColorPicker.Dialog;
using System.Windows.Media;

namespace IronworksTranslator.Utils.UI
{
    public class FF14ColorPickerButton : ColorPickerButtonBase
    {
        protected override void ChangeColor()
        {
            ColorPickerDialog dialog = new()
            {
                Owner = Owner,
                Color = Color,
                Colors = ColorsList,
            };

            var res = dialog.ShowDialog();

            if (res == true)
                Color = dialog.Color;
        }

        private static readonly string[] colors = [
            "#FFFFFF","#FFBDBD","#FFDEC7","#FFF7B0","#E8FFE0","#E5FFFC","#9CCEF2","#FFDBFF", "Black", "Black",
            "#F7F7F7","Red","#FF8000","Yellow","Lime","Cyan","Blue","Fuchsia", "Black", "Black",
            "#DEDEDE","#FF4A4A","#FFA666","#FFFFB0","#80FF00","#BAFFF0","#0080FF","#E05E8F", "Black", "Black",
            "#D6D6D6","#FF7D7D","#FFCCAB","#FFDE73","#80F75E","#66E5FF","#94BFFF","#FF8AC4", "Black", "Black",
            "#CCCCCC","#FFBFBF","#FF6600","#F0C76B","#D4FF7D","#ABDBE5","#8080FF","#FFB8DE", "Black", "Black",
            "#BDBDBD","#D6BFBF","#D6666B","#CCCC66","#ABD647","#B0E5E5","#B28AFF","#DEA6BA", "Black", "Black",
            "#A6A6A6","#C4A1A1","#D6BDAB","#C7BF9E","#38E5B2","#3BE5E5","#DEBFF7","#DE87F2", "Black", "Black",
        ];
        public List<Color> ColorsList { 
            get
            {
                if (_colorsList == null)
                {
                    _colorsList = [];
                    foreach (var color in colors)
                    {
                        _colorsList.Add((Color)ColorConverter.ConvertFromString(color));
                    }
                }
                return _colorsList;
            }
        }
        private List<Color>? _colorsList;
    }
}
