using System.Windows;
using OfficeMetaCleaner.Core;

namespace OfficeMetaCleaner.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        L10n.Apply(LanguageStorage.Load());
        base.OnStartup(e);
    }
}
