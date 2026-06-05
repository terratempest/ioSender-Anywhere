namespace CNC.Controls.Lathe;

public enum LatheWizardType
{
    None = 0,
    Parting,
    Threading,
    Turning,
    Facing
}

public interface ILatheWizardTab
{
    LatheWizardType LatheWizardType { get; }
    void Activate(bool activate);
}
