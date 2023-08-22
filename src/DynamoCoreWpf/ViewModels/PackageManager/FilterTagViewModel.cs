using System.Windows.Media;
using Dynamo.Core;

namespace ViewModels.PackageManager
{

    internal class FilterTagViewModel : NotificationObject
    {
        #region Private Fields
        private bool isFilterOn = false;
        #endregion

        #region Public Properties
        /// <summary>
        /// Title of the Filter
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Image icon associated with the Filter
        /// </summary>
        public ImageSource FilterImage { get; internal set; }

        /// <summary>
        /// The toggle that controls the visibility of the Filtered elements
        /// </summary>
        public bool IsFilterOn
        {
            get => isFilterOn;
            internal set
            {
                if (isFilterOn == value) return;
                isFilterOn = value;
                RaisePropertyChanged(nameof(IsFilterOn));
            }
        }
        #endregion

        public FilterTagViewModel()
        {
        }

        internal void Toggle(object obj)
        {
            IsFilterOn = !IsFilterOn;
        }
    }
}
