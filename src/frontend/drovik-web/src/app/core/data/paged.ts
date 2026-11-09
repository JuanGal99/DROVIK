import { HttpClient, HttpParams } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, debounce, EMPTY, expand, Observable, of, reduce, Subject, switchMap, timer } from 'rxjs';

export interface PagedResponse<T> { items: T[]; totalCount: number; page: number; pageSize: number; }
export interface StatusSummary { totalCount: number; byStatus: Record<string, number>; }
export const NO_SUMMARY: StatusSummary = { totalCount: 0, byStatus: {} };

const API_PAGE_LIMIT = 100;
const TYPING_WAIT_MS = 250;

/** For small catalogs that selectors and lookups need complete, such as vehicles and drivers. */
export function fetchAllPages<T>(http: HttpClient, url: string): Observable<T[]> {
  const fetchPage = (page: number) => http.get<PagedResponse<T>>(url, { params: new HttpParams().set('page', page).set('pageSize', API_PAGE_LIMIT) });
  return fetchPage(1).pipe(
    expand((response) => response.page * response.pageSize < response.totalCount ? fetchPage(response.page + 1) : EMPTY),
    reduce((items, response) => [...items, ...response.items], [] as T[]),
  );
}

export interface Reloader { now(): void; afterTyping(): void; }

/**
 * Keeps a screen's data current: only the newest request counts, so a slow old answer cannot overwrite a newer one.
 * Must be created while the component is being constructed. Reloads caused by `changes` run in the background.
 */
export function createReloader(
  load: (background: boolean) => Observable<unknown>,
  options: { changes?: Observable<void>; loaded?: () => void } = {},
): Reloader {
  const requests = new Subject<{ wait: number; background: boolean }>();
  requests.pipe(
    debounce(({ wait }) => wait ? timer(wait) : of(0)),
    switchMap(({ background }) => load(background).pipe(catchError(() => EMPTY))),
    takeUntilDestroyed(),
  ).subscribe(() => options.loaded?.());
  options.changes?.pipe(takeUntilDestroyed()).subscribe(() => requests.next({ wait: 0, background: true }));
  return {
    now: () => requests.next({ wait: 0, background: false }),
    afterTyping: () => requests.next({ wait: TYPING_WAIT_MS, background: false }),
  };
}
