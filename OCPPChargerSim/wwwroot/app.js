const vehicleStatusEl = document.getElementById("vehicle-status");
const configTableBody = document.getElementById("config-table");
const logOutput = document.getElementById("log-output");
const connectionStatusEl = document.getElementById("connection-status");
const statusButtons = document.querySelectorAll("[data-status]");
const clearLogsBtn = document.getElementById("clear-logs");
const copyLogsBtn = document.getElementById("copy-logs");
const metricPowerEl = document.getElementById("metric-power");
const metricCurrentEl = document.getElementById("metric-current");
const metricEnergyEl = document.getElementById("metric-energy");
const metricSoCEl = document.getElementById("metric-soc");
const heartbeatBtn = document.getElementById("send-heartbeat");
const closeBtn = document.getElementById("close-connection");
const logToggle = document.getElementById("toggle-log");
const configModal = document.getElementById("config-modal");
const configForm = document.getElementById("config-form");
const configUrlInput = document.getElementById("config-url");
const configIdentityInput = document.getElementById("config-identity");
const configAuthInput = document.getElementById("config-auth");
const configErrorEl = document.getElementById("config-error");
const configChargerSelect = document.getElementById("config-charger");
const configureBtn = document.getElementById("open-config");
const configChargePointSerialInput = document.getElementById("config-cp-serial");
const configChargeBoxSerialInput = document.getElementById("config-cb-serial");
const configCancelButton = document.getElementById("config-cancel");
const configMeterValuesSampledInput = document.getElementById("config-meter-values-sampled");
const configMeterValueSampleIntervalInput = document.getElementById("config-meter-value-sample-interval");
const configClockAlignedDataIntervalInput = document.getElementById("config-clock-aligned-data-interval");

let logs = [];
let configuration = {};
let metrics = null;
let connectionDetails = { url: "—", identity: "—", authKey: "—" };
let suppressLogToggleEvent = false;
let configurationRequired = false;
let chargers = [];
let selectedChargerId = null;
let chargePointSerialNumber = "0";
let chargeBoxSerialNumber = "0";
let meterValuesSampledData = "";
let meterValueSampleInterval = 0;
let clockAlignedDataInterval = 0;

const maxLogs = 500;

function isValidCentralSystemUrl(value) {
  if (!value) {
    return false;
  }

  try {
    const parsed = new URL(value);
    return parsed.protocol === "ws:" || parsed.protocol === "wss:";
  } catch (
    /** @type {unknown} */ _err
  ) {
    return false;
  }
}

function renderLogs() {
  logOutput.innerHTML = logs.map(formatLogLine).join("");
  logOutput.scrollTop = logOutput.scrollHeight;
}

function formatLogLine(line) {
  const escaped = escapeHtml(line);
  const lower = line.toLowerCase();

  if (
    (lower.includes("error") && !lower.includes("noerror")) ||
    lower.includes("not implemented") ||
    lower.includes("not supported") ||
    lower.includes("unsupported")
  ) {
    return `<span class="log-line log-error">${escaped}</span>`;
  }

  if (line.includes(" - SEND ")) {
    return `<span class="log-line log-send">${escaped}</span>`;
  }

  if (line.includes(" - RECV ")) {
    return `<span class="log-line log-recv">${escaped}</span>`;
  }

  return `<span class="log-line">${escaped}</span>`;
}

function escapeHtml(text) {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

function updateVehicleStatus(state) {
  vehicleStatusEl.textContent = state;
  const baseState = state.includes(" (") ? state.slice(0, state.indexOf(" (")) : state;
  vehicleStatusEl.dataset.status = baseState.trim().toLowerCase();

  const lowered = baseState.trim().toLowerCase();
  if (lowered !== "charging" && lowered !== "preparing") {
    clearMetricsDisplay();
  }
}

function formatNumber(value, digits = 1) {
  return Number.isFinite(value) ? value.toFixed(digits) : "—";
}

function clearMetricsDisplay() {
  metrics = null;
  metricPowerEl.textContent = "—";
  metricCurrentEl.textContent = "—";
  metricEnergyEl.textContent = "—";
  if (metricSoCEl) {
    metricSoCEl.textContent = "—";
  }
}

function renderChargerOptions() {
  if (!configChargerSelect) {
    return;
  }

  configChargerSelect.innerHTML = "";

  chargers.forEach((charger) => {
    const option = document.createElement("option");
    option.value = charger.id;
    option.textContent = `${charger.make} ${charger.model}`.trim();
    configChargerSelect.append(option);
  });

  if (!selectedChargerId && chargers.length > 0) {
    selectedChargerId = chargers[0].id;
  }

  if (selectedChargerId && chargers.some((c) => c.id === selectedChargerId)) {
    configChargerSelect.value = selectedChargerId;
  } else if (chargers.length > 0) {
    configChargerSelect.value = chargers[0].id;
  }
}

function populateConfigurationForm() {
  if (!configUrlInput || !configIdentityInput || !configAuthInput) {
    return;
  }

  configUrlInput.value = connectionDetails.url && connectionDetails.url !== "—" ? connectionDetails.url : "";
  configIdentityInput.value = connectionDetails.identity && connectionDetails.identity !== "—" ? connectionDetails.identity : "";
  configAuthInput.value = connectionDetails.authKey && connectionDetails.authKey !== "—" ? connectionDetails.authKey : "";

  if (configChargerSelect) {
    if (!selectedChargerId && chargers.length > 0) {
      selectedChargerId = chargers[0].id;
    }

    if (selectedChargerId && chargers.some((c) => c.id === selectedChargerId)) {
      configChargerSelect.value = selectedChargerId;
    } else if (chargers.length > 0) {
      configChargerSelect.value = chargers[0].id;
    }
  }

  if (configChargePointSerialInput) {
    configChargePointSerialInput.value = chargePointSerialNumber ?? "0";
  }

  if (configChargeBoxSerialInput) {
    configChargeBoxSerialInput.value = chargeBoxSerialNumber ?? "0";
  }

  if (configMeterValuesSampledInput) {
    configMeterValuesSampledInput.value = meterValuesSampledData ?? "";
  }

  if (configMeterValueSampleIntervalInput) {
    configMeterValueSampleIntervalInput.value = meterValueSampleInterval > 0 ? meterValueSampleInterval : "";
  }

  if (configClockAlignedDataIntervalInput) {
    configClockAlignedDataIntervalInput.value = clockAlignedDataInterval > 0 ? clockAlignedDataInterval : "";
  }
}

function showConfigurationModal() {
  if (!configModal) {
    return;
  }

  renderChargerOptions();
  populateConfigurationForm();
  clearConfigurationValidation();
  configModal.classList.remove("hidden");
}

function hideConfigurationModal() {
  if (!configModal) {
    return;
  }

  configModal.classList.add("hidden");
}

function clearConfigurationValidation() {
  if (configErrorEl) {
    configErrorEl.textContent = "";
  }

  [
    configUrlInput,
    configIdentityInput,
    configAuthInput,
    configChargerSelect,
    configChargePointSerialInput,
    configChargeBoxSerialInput,
    configMeterValuesSampledInput,
    configMeterValueSampleIntervalInput,
    configClockAlignedDataIntervalInput,
  ].forEach((input) => {
    input?.classList.remove("input-error");
  });
}

function showConfigurationError(message, input) {
  if (configErrorEl) {
    configErrorEl.textContent = message;
  }

  if (input) {
    input.classList.add("input-error");
    input.focus();
  }
}

function setConfigurationRequirement(required) {
  configurationRequired = required;
  if (configCancelButton) {
    configCancelButton.classList.toggle("hidden", required);
  }
  if (required) {
    showConfigurationModal();
  } else {
    hideConfigurationModal();
  }
}

function renderConfiguration() {
  if (!configTableBody) {
    return;
  }

  configTableBody.innerHTML = "";
  const reserved = new Set(["url", "identity", "authkey", "chargerid", "chargepointserialnumber", "chargeboxserialnumber"]);
  const entries = [];

  if (connectionDetails) {
    entries.push(["Url", connectionDetails.url ?? "—"]);
    entries.push(["Identity", connectionDetails.identity ?? "—"]);
    entries.push(["AuthKey", connectionDetails.authKey ?? "—"]);

    if (selectedChargerId) {
      const selected = chargers.find((c) => c.id === selectedChargerId);
      const label = selected ? `${selected.make} ${selected.model}`.trim() : selectedChargerId;
      entries.push(["Charger", label || selectedChargerId]);
    }
  }

  if (configuration && typeof configuration === "object") {
    const configEntries = Object.entries(configuration)
      .filter(([key]) => Boolean(key) && !reserved.has(key.toLowerCase()))
      .sort(([a], [b]) => a.localeCompare(b, undefined, { sensitivity: "base" }));
    entries.push(...configEntries);
  }

  entries.push(["Charge Point Serial", chargePointSerialNumber ?? "0"]);
  entries.push(["Charge Box Serial", chargeBoxSerialNumber ?? "0"]);
  if (meterValuesSampledData) {
    entries.push(["Meter Values Sampled Data", meterValuesSampledData]);
  }
  if (meterValueSampleInterval > 0) {
    entries.push(["Meter Value Sample Interval", `${meterValueSampleInterval} seconds`]);
  }
  if (clockAlignedDataInterval > 0) {
    entries.push(["Clock Aligned Data Interval", `${clockAlignedDataInterval} seconds`]);
  }

  for (const [key, value] of entries) {
    const row = document.createElement("tr");
    const keyCell = document.createElement("td");
    keyCell.textContent = key;
    const valueCell = document.createElement("td");
    valueCell.textContent = value;
    row.append(keyCell, valueCell);
    configTableBody.append(row);
  }
}

function updateMetrics(snapshot) {
  metrics = snapshot;
  if (!snapshot) {
    clearMetricsDisplay();
    return;
  }

  metricPowerEl.textContent = `${formatNumber(snapshot.powerKw, 1)} kW`;
  metricCurrentEl.textContent = `${formatNumber(snapshot.currentAmps, 0)} A`;
  metricEnergyEl.textContent = `${formatNumber(snapshot.energyWh / 1000, 2)} kWh`;
  if (metricSoCEl) {
    metricSoCEl.textContent = snapshot.stateOfCharge != null
      ? `${formatNumber(snapshot.stateOfCharge, 0)} %`
      : "—";
  }
}

function setConnectionStatus(state, message) {
  connectionStatusEl.textContent = message;
  connectionStatusEl.classList.remove("connecting", "connected", "disconnected", "error");
  connectionStatusEl.classList.add(state);
}

async function loadSnapshot() {
  try {
    const response = await fetch("/api/state");
    if (!response.ok) {
      throw new Error(`Failed to load state: ${response.status}`);
    }

    const snapshot = await response.json();
    if (Array.isArray(snapshot.chargers)) {
      chargers = snapshot.chargers.map((item) => ({
        id: item.id,
        make: item.make,
        model: item.model,
        chargePointModel: item.chargePointModel,
        chargePointVendor: item.chargePointVendor,
      }));
    }

    if (typeof snapshot.selectedCharger === "string") {
      selectedChargerId = snapshot.selectedCharger;
    } else if (!selectedChargerId && chargers.length > 0) {
      selectedChargerId = chargers[0].id;
    }

    if (snapshot.connection) {
      connectionDetails = {
        url: snapshot.connection.url ?? "—",
        identity: snapshot.connection.identity ?? "—",
        authKey: snapshot.connection.authKey ?? "—",
      };
    }

    if (snapshot.serialNumbers) {
      chargePointSerialNumber = snapshot.serialNumbers.chargePointSerial ?? "0";
      chargeBoxSerialNumber = snapshot.serialNumbers.chargeBoxSerial ?? "0";
    }

    if (snapshot.meterValuesConfiguration) {
      const meterConfig = snapshot.meterValuesConfiguration;
      meterValuesSampledData = meterConfig.meter_values_sampled_data ?? meterConfig.sampledData ?? "";
      const parsedSampleInterval = Number(
        meterConfig.meter_value_sample_interval ?? meterConfig.sampleInterval ?? 0,
      );
      meterValueSampleInterval = Number.isFinite(parsedSampleInterval) ? parsedSampleInterval : 0;

      const parsedClockAlignedInterval = Number(
        meterConfig.clock_aligned_data_interval ?? meterConfig.clockAlignedDataInterval ?? 0,
      );
      clockAlignedDataInterval = Number.isFinite(parsedClockAlignedInterval) ? parsedClockAlignedInterval : 0;
    }

    if (typeof snapshot.loggingEnabled === "boolean") {
      applyLoggingState(snapshot.loggingEnabled);
    }

    if (Array.isArray(snapshot.logs)) {
      logs = snapshot.logs.slice(-maxLogs);
      renderLogs();
    }

    if (snapshot.configuration) {
      configuration = snapshot.configuration;
    }
    renderConfiguration();

    if (snapshot.vehicleState) {
      updateVehicleStatus(snapshot.vehicleState);
    }

    if (snapshot.metrics) {
      updateMetrics(snapshot.metrics);
    }

    renderChargerOptions();
    populateConfigurationForm();

    if (typeof snapshot.requiresConfiguration === "boolean") {
      setConfigurationRequirement(snapshot.requiresConfiguration);
    }
  } catch (error) {
    console.error(error);
    setConnectionStatus("error", "Failed to load snapshot");
  }
}

function applyLoggingState(enabled) {
  if (!logToggle) {
    return;
  }

  suppressLogToggleEvent = true;
  logToggle.checked = Boolean(enabled);
  suppressLogToggleEvent = false;
}

async function postJson(url, payload) {
  const response = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    const problem = await response.json().catch(() => ({}));
    const message = problem.error ?? `Request failed (${response.status})`;
    throw new Error(message);
  }
}

function setupControls(connection) {
  statusButtons.forEach((button) => {
    button.addEventListener("click", async () => {
      const status = button.dataset.status;
      try {
        await postJson("/api/status", { status });
      } catch (error) {
        alert(error.message);
      }
    });
  });

  clearLogsBtn?.addEventListener("click", () => {
    logs = [];
    renderLogs();
  });

  copyLogsBtn?.addEventListener("click", async () => {
    try {
      if (!navigator.clipboard) {
        throw new Error("Clipboard API not available");
      }
      await navigator.clipboard.writeText(logs.join("\n"));
    } catch (error) {
      console.error("Failed to copy logs", error);
      alert("Unable to copy logs to clipboard.");
    }
  });

  heartbeatBtn?.addEventListener("click", async () => {
    heartbeatBtn.disabled = true;
    heartbeatBtn.textContent = "Sending…";
    try {
      await postJson("/api/heartbeat", {});
    } catch (error) {
      console.error(error);
      alert(error.message ?? "Failed to send heartbeat");
    } finally {
      heartbeatBtn.disabled = false;
      heartbeatBtn.textContent = "Send Heartbeat";
    }
  });

  closeBtn?.addEventListener("click", async () => {
    closeBtn.disabled = true;
    const original = closeBtn.textContent;
    closeBtn.textContent = "Closing…";
    try {
      await postJson("/api/close", {});
    } catch (error) {
      console.error(error);
      alert(error.message ?? "Failed to close connection");
    } finally {
      closeBtn.disabled = false;
      closeBtn.textContent = original ?? "Close Connection";
    }
  });

  logToggle?.addEventListener("change", async () => {
    if (suppressLogToggleEvent) {
      return;
    }

    const desiredState = logToggle.checked;
    try {
      await postJson("/api/logging", { enabled: desiredState });
    } catch (error) {
      console.error(error);
      alert(error.message ?? "Failed to update logging state");
      applyLoggingState(!desiredState);
    }
  });

  configureBtn?.addEventListener("click", () => {
    populateConfigurationForm();
    showConfigurationModal();
  });

  configCancelButton?.addEventListener("click", () => {
    if (configurationRequired) {
      return;
    }
    hideConfigurationModal();
  });

  configForm?.addEventListener("submit", async (event) => {
    event.preventDefault();
    if (!configUrlInput || !configIdentityInput || !configAuthInput || !configChargerSelect) {
      return;
    }

    clearConfigurationValidation();

    const urlValue = configUrlInput.value.trim();
    const identityValue = configIdentityInput.value.trim();
    const authKeyValue = configAuthInput.value.trim();
    const chargerValue = configChargerSelect.value.trim();
    const cpSerialValue = configChargePointSerialInput ? configChargePointSerialInput.value.trim() : "0";
    const cbSerialValue = configChargeBoxSerialInput ? configChargeBoxSerialInput.value.trim() : "0";
    const meterValuesCsv = configMeterValuesSampledInput ? configMeterValuesSampledInput.value.trim() : "";
    const sampleIntervalValue = configMeterValueSampleIntervalInput
      ? Number.parseInt(configMeterValueSampleIntervalInput.value, 10)
      : NaN;
    const clockAlignedIntervalValue = configClockAlignedDataIntervalInput
      ? Number.parseInt(configClockAlignedDataIntervalInput.value, 10)
      : NaN;

    if (!isValidCentralSystemUrl(urlValue)) {
      showConfigurationError("Please enter a valid ws:// or wss:// URL.", configUrlInput);
      return;
    }

    if (!chargerValue) {
      showConfigurationError("Please select a charger type.", configChargerSelect);
      return;
    }

    if (!Number.isFinite(sampleIntervalValue) || sampleIntervalValue <= 0) {
      showConfigurationError(
        "Meter Value Sample Interval must be greater than zero.",
        configMeterValueSampleIntervalInput,
      );
      return;
    }

    if (!Number.isFinite(clockAlignedIntervalValue) || clockAlignedIntervalValue <= 0) {
      showConfigurationError(
        "Clock Aligned Data Interval must be greater than zero.",
        configClockAlignedDataIntervalInput,
      );
      return;
    }

    const payload = {
      url: urlValue,
      identity: identityValue,
      authKey: authKeyValue,
      chargerId: chargerValue,
      chargePointSerialNumber: cpSerialValue,
      chargeBoxSerialNumber: cbSerialValue,
      meterValuesSampledData: meterValuesCsv,
      meterValueSampleInterval: sampleIntervalValue,
      clockAlignedDataInterval: clockAlignedIntervalValue,
    };

    const submitButton = configForm.querySelector("button[type='submit']");
    if (submitButton) {
      submitButton.disabled = true;
    }

    try {
      selectedChargerId = chargerValue;
      chargePointSerialNumber = cpSerialValue || "0";
      chargeBoxSerialNumber = cbSerialValue || "0";
      meterValuesSampledData = meterValuesCsv;
      meterValueSampleInterval = sampleIntervalValue;
      clockAlignedDataInterval = clockAlignedIntervalValue;
      await postJson("/api/bootstrap", payload);
      await loadSnapshot();
      setConfigurationRequirement(false);
    } catch (error) {
      console.error(error);
      if (configErrorEl) {
        configErrorEl.textContent = error.message ?? "Failed to save configuration";
      }
      setConfigurationRequirement(true);
    } finally {
      if (submitButton) {
        submitButton.disabled = false;
      }
    }
  });

  connection.on("LogAdded", (message) => {
    logs.push(message);
    if (logs.length > maxLogs) {
      logs = logs.slice(-maxLogs);
    }
    renderLogs();
  });

  connection.on("LogSnapshot", (entries) => {
    if (Array.isArray(entries)) {
      logs = entries.slice(-maxLogs);
      renderLogs();
    }
  });

  connection.on("VehicleStateChanged", (state) => {
    updateVehicleStatus(state);
  });

  connection.on("ConfigurationUpdated", (key, value) => {
    if (!key) {
      return;
    }

    const lowered = key.toLowerCase();
    if (lowered === "url") {
      connectionDetails = { ...connectionDetails, url: value };
    } else if (lowered === "identity") {
      connectionDetails = { ...connectionDetails, identity: value };
    } else if (lowered === "authkey") {
      connectionDetails = { ...connectionDetails, authKey: value };
    } else {
      configuration[key] = value;
    }

    renderConfiguration();
  });

  connection.on("ConfigurationSnapshot", (config) => {
    if (config && typeof config === "object") {
      const reserved = new Set(["url", "identity", "authkey"]);
      const cleaned = {};

      for (const [key, value] of Object.entries(config)) {
        const lowered = key.toLowerCase();
        if (reserved.has(lowered)) {
          if (lowered === "url") {
            connectionDetails = { ...connectionDetails, url: value };
          } else if (lowered === "identity") {
            connectionDetails = { ...connectionDetails, identity: value };
          } else if (lowered === "authkey") {
            connectionDetails = { ...connectionDetails, authKey: value };
          }
        } else {
          cleaned[key] = value;
        }
      }

      configuration = cleaned;
      renderConfiguration();
    }
  });

  connection.on("MeterValuesUpdated", (sample) => {
    if (sample && typeof sample === "object") {
      updateMetrics(sample);
    }
  });

  connection.onreconnecting(() => {
    setConnectionStatus("connecting", "Reconnecting…");
  });

  connection.onreconnected(() => {
    setConnectionStatus("connected", "Connected");
  });

  connection.onclose(() => {
    setConnectionStatus("disconnected", "Disconnected");
  });
}

async function start() {
  setConnectionStatus("connecting", "Connecting…");
  await loadSnapshot();

  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hub/simulator")
    .withAutomaticReconnect()
    .build();

  setupControls(connection);

  try {
    await connection.start();
    setConnectionStatus("connected", "Connected");
  } catch (error) {
    console.error("Failed to establish SignalR connection", error);
    setConnectionStatus("error", "Connection failed");
  }
}

start();
