export interface AuthConfig {
  enabled: boolean;
  clientId?: string;
  tenantId?: string;
  instance?: string;
  scopes?: string[];
}
