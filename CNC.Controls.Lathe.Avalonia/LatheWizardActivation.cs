using CNC.Core;

namespace CNC.Controls.Lathe;

internal static class LatheWizardActivation
{
    public static void OnActivate(BaseViewModel model, ref bool initOk)
    {
        if (!GrblSettings.IsLoaded)
            return;

        if (!initOk)
        {
            initOk = true;
            model.Profiles = model.wz.Load();
            model.config.Update();
            LatheConverters.IsMetric = model.IsMetric = GrblParserState.IsMetric;
            model.XStart = model.IsMetric ? 10.0d : 0.5d;
        }
        else
        {
            model.gCode.Clear();
            model.PassData.Clear();
        }
    }
}
