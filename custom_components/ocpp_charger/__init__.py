"""Home Assistant integration for the OCPP Charger simulator."""

from __future__ import annotations

import logging
from typing import Any, Callable

from homeassistant.config_entries import ConfigEntry
from homeassistant.core import HomeAssistant, State, callback
from homeassistant.helpers.aiohttp_client import async_get_clientsession
from homeassistant.helpers.event import async_track_state_change_event

from .const import (
    CONF_BASE_URL,
    CONF_CONNECTED_STATUS,
    CONF_CONNECTED_VALUE,
    CONF_CURRENT_ENTITY,
    CONF_DISCONNECTED_STATUS,
    CONF_DISCONNECTED_VALUE,
    CONF_ENERGY_ENTITY,
    CONF_POWER_ENTITY,
    CONF_SOC_ENTITY,
    CONF_STATUS_ENTITY,
    DEFAULT_BASE_URL,
    DEFAULT_CONNECTED_STATUS,
    DEFAULT_CONNECTED_VALUE,
    DEFAULT_DISCONNECTED_STATUS,
    DEFAULT_DISCONNECTED_VALUE,
    DOMAIN,
)

_LOGGER = logging.getLogger(__name__)


async def async_setup_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Set up the integration from a config entry."""

    hass.data.setdefault(DOMAIN, {})

    client = OcppHttpClient(hass, entry)
    automation = OcppAutomation(hass, entry, client)
    await automation.async_start()

    hass.data[DOMAIN][entry.entry_id] = automation

    return True


async def async_unload_entry(hass: HomeAssistant, entry: ConfigEntry) -> bool:
    """Unload a config entry."""

    automation: OcppAutomation | None = hass.data.get(DOMAIN, {}).pop(entry.entry_id, None)
    if automation:
        automation.async_unload()

    return True


class OcppHttpClient:
    """Simple HTTP client for the simulator API."""

    def __init__(self, hass: HomeAssistant, entry: ConfigEntry) -> None:
        self._hass = hass
        self._entry = entry
        self._session = async_get_clientsession(hass)

    @property
    def base_url(self) -> str:
        """Return the base URL for the simulator."""

        options = self._entry.options or {}
        return options.get(CONF_BASE_URL) or self._entry.data.get(CONF_BASE_URL, DEFAULT_BASE_URL)

    async def async_post(self, path: str, payload: dict[str, Any]) -> None:
        """Post JSON data to the simulator, logging any issues."""

        url = f"{self.base_url.rstrip('/')}{path}"
        try:
            async with self._session.post(url, json=payload, timeout=10) as response:
                if response.status >= 400:
                    _LOGGER.warning("Simulator API call to %s failed: %s", url, response.status)
        except Exception as err:  # pylint: disable=broad-except
            _LOGGER.error("Error sending data to simulator at %s: %s", url, err)

    async def async_send_metrics(self, payload: dict[str, Any]) -> None:
        """Send metric updates to the simulator."""

        await self.async_post("/api/metrics", payload)

    async def async_send_status(self, status: str) -> None:
        """Send a manual status change to the simulator."""

        await self.async_post("/api/status", {"status": status})


class OcppAutomation:
    """Handle state tracking for metric and status mirroring."""

    def __init__(self, hass: HomeAssistant, entry: ConfigEntry, client: OcppHttpClient) -> None:
        self._hass = hass
        self._entry = entry
        self._client = client
        self._unsubscribers: list[Callable[[], None]] = []

    @property
    def data(self) -> dict:
        """Shortcut for merged entry data and options."""

        return {**self._entry.data, **self._entry.options}

    async def async_start(self) -> None:
        """Register entity listeners and push initial state."""

        metrics = [
            (self.data.get(CONF_ENERGY_ENTITY), "energyWh"),
            (self.data.get(CONF_POWER_ENTITY), "powerKw"),
            (self.data.get(CONF_CURRENT_ENTITY), "currentAmps"),
            (self.data.get(CONF_SOC_ENTITY), "stateOfCharge"),
        ]

        for entity_id, _ in metrics:
            if entity_id:
                self._unsubscribers.append(
                    async_track_state_change_event(
                        self._hass, [entity_id], self._async_handle_metric_change
                    )
                )

        status_entity = self.data.get(CONF_STATUS_ENTITY)
        if status_entity:
            self._unsubscribers.append(
                async_track_state_change_event(self._hass, [status_entity], self._async_handle_status_change)
            )

        await self._async_push_metrics()
        await self._async_push_status()

    async def _async_push_metrics(self) -> None:
        payload = self._build_metric_payload()
        if payload:
            await self._client.async_send_metrics(payload)

    async def _async_push_status(self) -> None:
        status = self._calculate_status_from_entity()
        if status:
            await self._client.async_send_status(status)

    def _build_metric_payload(self) -> dict[str, float]:
        payload: dict[str, float] = {}

        entity_mapping = {
            "energyWh": self.data.get(CONF_ENERGY_ENTITY),
            "powerKw": self.data.get(CONF_POWER_ENTITY),
            "currentAmps": self.data.get(CONF_CURRENT_ENTITY),
            "stateOfCharge": self.data.get(CONF_SOC_ENTITY),
        }

        for key, entity_id in entity_mapping.items():
            if not entity_id:
                continue

            value = self._coerce_float(self._hass.states.get(entity_id))
            if value is not None:
                payload[key] = value

        return payload

    @staticmethod
    def _coerce_float(state: State | None) -> float | None:
        if state is None:
            return None

        try:
            return float(state.state)
        except (TypeError, ValueError):
            return None

    @callback
    async def _async_handle_metric_change(self, event) -> None:
        await self._async_push_metrics()

    def _calculate_status_from_entity(self) -> str | None:
        entity_id = self.data.get(CONF_STATUS_ENTITY)
        if not entity_id:
            return None

        state = self._hass.states.get(entity_id)
        if state is None:
            return None

        connected_value = self.data.get(CONF_CONNECTED_VALUE, DEFAULT_CONNECTED_VALUE)
        disconnected_value = self.data.get(CONF_DISCONNECTED_VALUE, DEFAULT_DISCONNECTED_VALUE)

        if state.state == connected_value:
            return self.data.get(CONF_CONNECTED_STATUS, DEFAULT_CONNECTED_STATUS)

        if state.state == disconnected_value:
            return self.data.get(CONF_DISCONNECTED_STATUS, DEFAULT_DISCONNECTED_STATUS)

        return None

    @callback
    async def _async_handle_status_change(self, event) -> None:
        status = self._calculate_status_from_entity()
        if status:
            await self._client.async_send_status(status)

    def async_unload(self) -> None:
        for unsub in self._unsubscribers:
            unsub()
        self._unsubscribers.clear()
