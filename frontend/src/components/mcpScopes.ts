// Plain-language labels for the permissions an AI assistant asks for (plan 011). Order = consent page order.
import type { TranslationKey } from "../i18n";
import type { McpScope } from "../types/api";

export const SCOPE_LABEL: Record<McpScope, TranslationKey> = {
  "offices:read": "mcpScopeOfficesRead",
  "bookings:read": "mcpScopeBookingsRead",
  "bookings:write": "mcpScopeBookingsWrite",
  "clients:basic": "mcpScopeClientsBasic",
  "clients:full": "mcpScopeClientsFull",
  "admin:read": "mcpScopeAdminRead",
  "admin:write": "mcpScopeAdminWrite",
};

/** The warning shown under a sensitive permission while it is ticked. */
export const SCOPE_WARNING: Partial<Record<McpScope, TranslationKey>> = {
  "clients:full": "mcpScopeClientsFullWarning",
  "admin:write": "mcpScopeAdminWriteWarning",
};
