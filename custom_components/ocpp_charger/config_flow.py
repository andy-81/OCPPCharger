"""Config flow for the OCPP Charger Simulator integration."""

from __future__ import annotations

import voluptuous as vol
from homeassistant import config_entries
from homeassistant.const import CONF_NAME
from homeassistant.data_entry_flow import FlowResult

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


class OcppChargerConfigFlow(config_entries.ConfigFlow, domain=DOMAIN):
    """Handle a config flow for the simulator."""

    VERSION = 1

    async def async_step_user(self, user_input: dict | None = None) -> FlowResult:
        """Handle the initial step."""
        errors: dict[str, str] = {}

        if user_input is not None:
            title = user_input.get(CONF_NAME) or "OCPP Charger"
            return self.async_create_entry(title=title, data=user_input)

        data_schema = vol.Schema(
            {
                vol.Optional(CONF_NAME, default="OCPP Charger"): str,
                vol.Required(CONF_BASE_URL, default=DEFAULT_BASE_URL): str,
                vol.Optional(CONF_ENERGY_ENTITY): str,
                vol.Optional(CONF_POWER_ENTITY): str,
                vol.Optional(CONF_CURRENT_ENTITY): str,
                vol.Optional(CONF_SOC_ENTITY): str,
                vol.Optional(CONF_STATUS_ENTITY): str,
                vol.Optional(CONF_CONNECTED_VALUE, default=DEFAULT_CONNECTED_VALUE): str,
                vol.Optional(CONF_DISCONNECTED_VALUE, default=DEFAULT_DISCONNECTED_VALUE): str,
                vol.Optional(CONF_CONNECTED_STATUS, default=DEFAULT_CONNECTED_STATUS): str,
                vol.Optional(CONF_DISCONNECTED_STATUS, default=DEFAULT_DISCONNECTED_STATUS): str,
            }
        )

        return self.async_show_form(step_id="user", data_schema=data_schema, errors=errors)

    async def async_get_options_flow(self, config_entry: config_entries.ConfigEntry) -> config_entries.OptionsFlow:
        """Return the options flow."""

        return OcppChargerOptionsFlow(config_entry)


class OcppChargerOptionsFlow(config_entries.OptionsFlow):
    """Handle the options for the integration."""

    def __init__(self, entry: config_entries.ConfigEntry) -> None:
        """Initialize options flow."""

        self.config_entry = entry

    async def async_step_init(self, user_input: dict | None = None) -> FlowResult:
        """Manage the options for the integration."""
        errors: dict[str, str] = {}

        if user_input is not None:
            return self.async_create_entry(title="", data=user_input)

        current = {**self.config_entry.data, **self.config_entry.options}

        data_schema = vol.Schema(
            {
                vol.Required(CONF_BASE_URL, default=current.get(CONF_BASE_URL, DEFAULT_BASE_URL)): str,
                vol.Optional(CONF_ENERGY_ENTITY, default=current.get(CONF_ENERGY_ENTITY, "")): str,
                vol.Optional(CONF_POWER_ENTITY, default=current.get(CONF_POWER_ENTITY, "")): str,
                vol.Optional(CONF_CURRENT_ENTITY, default=current.get(CONF_CURRENT_ENTITY, "")): str,
                vol.Optional(CONF_SOC_ENTITY, default=current.get(CONF_SOC_ENTITY, "")): str,
                vol.Optional(CONF_STATUS_ENTITY, default=current.get(CONF_STATUS_ENTITY, "")): str,
                vol.Optional(CONF_CONNECTED_VALUE, default=current.get(CONF_CONNECTED_VALUE, DEFAULT_CONNECTED_VALUE)): str,
                vol.Optional(
                    CONF_DISCONNECTED_VALUE, default=current.get(CONF_DISCONNECTED_VALUE, DEFAULT_DISCONNECTED_VALUE)
                ): str,
                vol.Optional(CONF_CONNECTED_STATUS, default=current.get(CONF_CONNECTED_STATUS, DEFAULT_CONNECTED_STATUS)): str,
                vol.Optional(
                    CONF_DISCONNECTED_STATUS,
                    default=current.get(CONF_DISCONNECTED_STATUS, DEFAULT_DISCONNECTED_STATUS),
                ): str,
            }
        )

        return self.async_show_form(step_id="init", data_schema=data_schema, errors=errors)
