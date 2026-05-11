using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace nsaygqv0ixdkwb
{
    public sealed partial class ErrorPage : Page
    {
        public ErrorPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            MessageText.Text = e.Parameter as string ?? "Unknown error";
        }
    }
}
