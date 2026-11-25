export const environment = {
  production: false,
  apiBaseUrl: 'https://localhost:7257',
  auth0: {
    domain: 'your-tenant.auth0.com',
    clientId: 'your-spa-client-id',
    audience: 'https://api.remba.local',
  },
  // Fixed overlay border so you can tell which environment is loaded.
  // Does not affect layout (pointer-events: none, position: fixed).
  devFrame: {
    enabled: true,
    widthPx: 3,
    color: '#ff6a00',
  },
};
