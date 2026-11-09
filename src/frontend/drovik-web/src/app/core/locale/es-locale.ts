import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';

// Imported for its side effect by every lazily loaded page that formats values with the 'es' locale.
registerLocaleData(localeEs, 'es');
