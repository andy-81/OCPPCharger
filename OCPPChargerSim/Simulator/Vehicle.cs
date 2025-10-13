namespace OcppSimulator;

public sealed class Vehicle
{
    public string State { get; private set; } = "Available";

    public void SetStatus(string status)
    {
        State = string.IsNullOrWhiteSpace(status) ? State : status;
    }
}
