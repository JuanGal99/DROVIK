import { HttpClient, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { fetchAllPages } from './paged';

describe('fetchAllPages', () => {
  it('keeps asking until it has every record', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const http = TestBed.inject(HttpTestingController);
    let result: number[] = [];
    fetchAllPages<number>(TestBed.inject(HttpClient), '/items').subscribe((items) => result = items);

    const first = http.expectOne((request) => request.params.get('page') === '1');
    expect(first.request.params.get('pageSize')).toBe('100');
    first.flush({ items: [1, 2], totalCount: 101, page: 1, pageSize: 100 });
    http.expectOne((request) => request.params.get('page') === '2').flush({ items: [3], totalCount: 101, page: 2, pageSize: 100 });

    expect(result).toEqual([1, 2, 3]);
    http.verify();
  });
});
