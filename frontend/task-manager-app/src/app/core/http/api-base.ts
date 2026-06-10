import { environment } from '../../../environments/environment';

export const API_BASE = environment.apiUrl;

export const apiUrl = (path: string): string => `${API_BASE}${path}`;
