using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IronworksTranslator.ViewModels.Pages
{
    public partial class DeveloperViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isDraggable = true;
    }
}
