using GameFramework;

namespace LOP
{
    public class AppearanceComponent : LOPComponent
    {
        private string _visualId;
        public string visualId
        {
            get => _visualId;
            set
            {
                this.SetProperty(ref _visualId, value, entity.RaisePropertyChanged);
            }
        }

        public void Initialize(string visualId)
        {
            this.visualId = visualId;
        }
    }
}
