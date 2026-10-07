/**
 * Caches a computed value until one of its dependencies changes (compared by reference).
 *
 * Use it for template getters that build arrays, so change detection gets the same array
 * back instead of a new one every pass (which would also re-render the child it is bound to):
 *
 *   private readonly cityOptionsMemo = memo<MultiSelectOption[]>();
 *   get cityOptions() { return this.cityOptionsMemo([this.cities], () => this.cities.map(...)); }
 *
 * Source lists must be replaced, not mutated in place, for the cache to notice the change.
 */
export function memo<T>(): (deps: readonly unknown[], compute: () => T) => T {
  let lastDeps: readonly unknown[] | null = null;
  let lastValue!: T;
  return (deps, compute) => {
    if (lastDeps && deps.length === lastDeps.length && deps.every((dep, i) => dep === lastDeps![i])) {
      return lastValue;
    }
    lastDeps = deps;
    lastValue = compute();
    return lastValue;
  };
}
