export const environment = {
  production: true,
  // Other devices on the local network reach the Gateway through the dev-server proxy (npm run start:lan).
  gatewayUrl: window.location.hostname === 'localhost' ? 'http://localhost:5200' : window.location.origin,
};
