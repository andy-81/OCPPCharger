# OCPP Charger Simulator

An Web application that simulates an OCPP 1.6 charge point with a built-in web dashboard for controlling vehicle state, streaming logs, and adjusting configuration.

## Features
- Real-time dashboard with log stream, status indicators, and manual control buttons.
- Configurable charger catalog with boot-notification identity data.
- Persistent meter state, optional SoC reporting, and heartbeat loop management.
- Browser modal for editing connection details, charger type, and serial numbers.

## Prerequisites
- .NET 9 SDK
- Modern browser for the dashboard UI

## Getting Started
```bash
cd OCPPChargerSim
dotnet build
dotnet run --project OCPPChargerSim/OCPPChargerSim.csproj
```
Access the dashboard at `http://localhost:5000`.

### Docker
```bash
docker build -t ocpp-charger-sim .
docker run --rm -p 5000:5000 ocpp-charger-sim
```
After the container starts, open `http://localhost:5000` in your browser.

To retain user settings, and logs specify a data directory:
```bash
docker run --rm -p 5000:5000 -v $(pwd)/sim-data:/app/data ocpp-charger-sim
```

On first launch, the configuration dialog appears automatically. Enter the OCPP server URL, charge point identity, authorization key, charger type, and serial numbers. The settings are saved to `simulator.json`.

## Files of Note
- `OCPPChargerSim/` – ASP.NET Core web host, SignalR hub, services, and static UI assets.
- `OCPPChargerSim/Simulator/` – OCPP simulator logic and supporting classes.
- `chargers.json` – Charger catalog (copied to output on build).
- `simulator.json` – User configuration generated at runtime (excluded from source control).

## License
This project is provided as-is without warranty.

## Credits
Written by Factory.AI
