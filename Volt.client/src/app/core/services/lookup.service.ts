import { Injectable, inject } from '@angular/core';
import { Observable, defer, shareReplay, timer } from 'rxjs';
import {
  CategoryClient,
  CategoryLookupDto,
  CityClient,
  DeliveryClient,
  DeliveryLookupDto,
  MerchantClient,
  MerchantLookupDto,
  PagedResultOfCityDto,
  SubCategoryClient,
  SubCategoryLookupDto,
  ZoneLookupDto
} from './clientAPI';

/** How long a list is reused before it is fetched again. */
const TTL_MS = 2 * 60 * 1000;

/**
 * Shared, short-lived cache for the lists that many screens load into dropdowns
 * (cities, zones, merchants, riders, categories). Opening several pages in a row
 * reuses one request instead of firing the same call on every screen.
 *
 * Any successful write (POST/PUT/DELETE) clears the cache (see LookupCacheInterceptor),
 * so a city or merchant added a moment ago shows up on the next screen.
 */
@Injectable({ providedIn: 'root' })
export class LookupService {
  private readonly cityClient = inject(CityClient);
  private readonly merchantClient = inject(MerchantClient);
  private readonly deliveryClient = inject(DeliveryClient);
  private readonly categoryClient = inject(CategoryClient);
  private readonly subCategoryClient = inject(SubCategoryClient);

  private readonly cache = new Map<string, Observable<unknown>>();

  /** Active cities (first 1000), same shape as CityClient.getAll. */
  activeCities(): Observable<PagedResultOfCityDto> {
    return this.cached('cities:active', () => this.cityClient.getAll(1, 1000, undefined, true));
  }

  /** All cities, active and inactive (reports and dashboard filters). */
  allCities(): Observable<PagedResultOfCityDto> {
    return this.cached('cities:all', () => this.cityClient.getAll(1, 1000));
  }

  zonesByCity(cityId: number): Observable<ZoneLookupDto[]> {
    return this.cached(`zones:${cityId}`, () => this.cityClient.getZonesByCity(cityId));
  }

  activeMerchants(): Observable<MerchantLookupDto[]> {
    return this.cached('merchants:active', () => this.merchantClient.getActive());
  }

  activeDeliveries(): Observable<DeliveryLookupDto[]> {
    return this.cached('deliveries:active', () => this.deliveryClient.getActive());
  }

  categories(): Observable<CategoryLookupDto[]> {
    return this.cached('categories', () => this.categoryClient.getLookup());
  }

  subCategories(): Observable<SubCategoryLookupDto[]> {
    return this.cached('subCategories', () => this.subCategoryClient.getLookup());
  }

  clear(): void {
    this.cache.clear();
  }

  private cached<T>(key: string, load: () => Observable<T>): Observable<T> {
    let entry = this.cache.get(key) as Observable<T> | undefined;
    if (!entry) {
      entry = defer(load).pipe(
        // Keep the result for TTL_MS; a failed request is not cached.
        shareReplay({ bufferSize: 1, refCount: false, windowTime: TTL_MS })
      );
      this.cache.set(key, entry);
      // Drop the entry after the TTL (and on error) so the next caller fetches fresh data.
      const drop = () => { if (this.cache.get(key) === entry) this.cache.delete(key); };
      timer(TTL_MS).subscribe(drop);
      entry.subscribe({ error: drop });
    }
    return entry;
  }
}
