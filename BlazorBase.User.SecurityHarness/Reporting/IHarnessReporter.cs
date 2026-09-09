namespace BlazorBase.User.SecurityHarness.Reporting;

/// <summary>Renders a <see cref="HarnessReport"/> to text in one specific format.</summary>
public interface IHarnessReporter
{
    string Render(HarnessReport report);
}
