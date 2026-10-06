import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { CityClient, CityDto, AddCityCommand, UpdateCityCommand, TieredDiscountDto, ZoneGroupLookupDto } from '../../../core/services/clientAPI';
import { LocaleService } from '../../../core/services/locale.service';
import {
  CityCommission,
  DEFAULT_CITY_COMMISSION,
  RiderDispatchService
} from '../../../core/services/rider-dispatch.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

/** Rider commission for both trips together cannot exceed the vehicle delivery fee. */
function commissionSumValidator(group: AbstractControl): ValidationErrors | null {
  const delivery = Number(group.get('deliveryLegCommissionPercent')?.value ?? 0) || 0;
  const ret = Number(group.get('returnLegCommissionPercent')?.value ?? 0) || 0;
  return delivery + ret > 100 ? { commissionOver100: true } : null;
}

@Component({
  selector: 'app-city-form',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterModule, TranslatePipe],
  templateUrl: './city-form.component.html',
  styleUrls: ['../../../shared/styles/entity-form.css', './city-form.component.css']
})
export class CityFormComponent implements OnInit {
  private readonly localeService = inject(LocaleService);
  private readonly dispatchService = inject(RiderDispatchService);

  cityForm: FormGroup;
  isEditMode = false;
  cityId: number | null = null;
  isLoading = false;
  isSaving = false;
  errorMessage = '';
  zoneGroups: ZoneGroupLookupDto[] = [];

  constructor(
    private cityClient: CityClient,
    private route: ActivatedRoute,
    private router: Router,
    private fb: FormBuilder
  ) {
    this.cityForm = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]],
      description: [null],
      zoneGroupId: [null, [Validators.required]],
      urgentDelivery: [0, [Validators.min(0)]],
      serviceFees: [0, [Validators.min(0)]],
      cancellationFees: [0, [Validators.min(0), Validators.max(100)]],
      deliveryLegCommissionPercent: [DEFAULT_CITY_COMMISSION.deliveryLegCommissionPercent, [Validators.required, Validators.min(0), Validators.max(100)]],
      returnLegCommissionPercent: [DEFAULT_CITY_COMMISSION.returnLegCommissionPercent, [Validators.required, Validators.min(0), Validators.max(100)]],
      tieredDiscounts: this.fb.array([])
    }, { validators: commissionSumValidator });
  }

  ngOnInit(): void {
    this.cityClient.getZoneGroups().subscribe({
      next: (groups) => {
        this.zoneGroups = groups || [];
      },
      error: () => {
        this.zoneGroups = [];
      }
    });

    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id && id !== 'new') {
        this.cityId = +id;
        this.isEditMode = true;
        this.loadCity();
      } else {
        this.isEditMode = false;
        this.cityId = null;
      }
    });
  }

  get commissionTotal(): number {
    const delivery = Number(this.cityForm.get('deliveryLegCommissionPercent')?.value ?? 0) || 0;
    const ret = Number(this.cityForm.get('returnLegCommissionPercent')?.value ?? 0) || 0;
    return Math.round((delivery + ret) * 100) / 100;
  }

  private get commissionValue(): CityCommission {
    return {
      deliveryLegCommissionPercent: Number(this.cityForm.get('deliveryLegCommissionPercent')?.value ?? 0) || 0,
      returnLegCommissionPercent: Number(this.cityForm.get('returnLegCommissionPercent')?.value ?? 0) || 0
    };
  }

  get tieredDiscountsFormArray(): FormArray {
    return this.cityForm.get('tieredDiscounts') as FormArray;
  }

  isZoneGroupSelected(zoneGroupId: number): boolean {
    return Number(this.cityForm.get('zoneGroupId')?.value) === Number(zoneGroupId);
  }

  selectZoneGroup(zoneGroupId: number): void {
    this.cityForm.patchValue({ zoneGroupId });
    this.cityForm.get('zoneGroupId')?.markAsTouched();
    this.cityForm.get('zoneGroupId')?.markAsDirty();
  }

  getTieredDiscountFormGroup(index: number): FormGroup {
    return this.tieredDiscountsFormArray.at(index) as FormGroup;
  }

  addTieredDiscount(from: number = 0, to: number = 0, discount: number = 0, id: number = 0): void {
    const tieredDiscountForm = this.fb.group({
      id: [id],
      from: [from, [Validators.min(0)]],
      to: [to, [Validators.min(0)]],
      discount: [discount, [Validators.min(0), Validators.max(100)]]
    }, {
      validators: (formGroup: FormGroup) => {
        const fromValue = formGroup.get('from')?.value ?? 0;
        const toValue = formGroup.get('to')?.value ?? 0;
        const discountValue = formGroup.get('discount')?.value ?? 0;

        formGroup.get('from')?.setErrors(null);
        formGroup.get('to')?.setErrors(null);
        formGroup.get('discount')?.setErrors(null);

        const isEmpty = (!fromValue || fromValue === 0) && (!toValue || toValue === 0) && (!discountValue || discountValue === 0);
        if (isEmpty) {
          return null;
        }

        const hasAnyValue = (fromValue && fromValue > 0) || (toValue && toValue > 0) || (discountValue && discountValue > 0);

        if (hasAnyValue) {
          let hasErrors = false;

          if (!fromValue || fromValue <= 0) {
            formGroup.get('from')?.setErrors({ required: true });
            hasErrors = true;
          }
          if (!toValue || toValue <= 0) {
            formGroup.get('to')?.setErrors({ required: true });
            hasErrors = true;
          }
          if (!discountValue && discountValue !== 0) {
            formGroup.get('discount')?.setErrors({ required: true });
            hasErrors = true;
          }

          if (fromValue > 0 && toValue > 0 && toValue < fromValue) {
            return { invalidRange: true };
          }

          if (hasErrors) {
            return { incomplete: true };
          }
        }
        return null;
      }
    });

    this.tieredDiscountsFormArray.push(tieredDiscountForm);
  }

  removeTieredDiscount(index: number): void {
    this.tieredDiscountsFormArray.removeAt(index);
  }

  /** Appends a tier that starts the day after the last one ends. */
  addNextTier(): void {
    const ends = this.tierRows.map(t => t.to).filter(v => v > 0);
    const from = ends.length ? Math.max(...ends) + 1 : 1;
    this.addTieredDiscount(from, from + 6, 0);
  }

  num(name: string): number {
    return Number(this.cityForm.get(name)?.value ?? 0) || 0;
  }

  /** Slider writes through the form control so the number box updates with it. */
  setPercent(name: string, value: string | number): void {
    const control = this.cityForm.get(name);
    control?.setValue(Number(value));
    control?.markAsDirty();
    control?.markAsTouched();
  }

  get tierRows(): { from: number; to: number; discount: number }[] {
    return this.tieredDiscountsFormArray.controls.map(c => ({
      from: Number(c.get('from')?.value ?? 0) || 0,
      to: Number(c.get('to')?.value ?? 0) || 0,
      discount: Number(c.get('discount')?.value ?? 0) || 0
    }));
  }

  /** Right edge of the discount ladder axis, in days. */
  get ladderMax(): number {
    const ends = this.tierRows.map(t => Math.max(t.to, t.from));
    return Math.max(30, ...ends) + 1;
  }

  /** Tallest discount on the ladder, so small percentages stay readable. */
  get ladderPeak(): number {
    return Math.min(100, Math.max(10, ...this.tierRows.map(t => t.discount)));
  }

  tierLeft(i: number): number {
    const t = this.tierRows[i];
    return (Math.max(t.from, 1) - 1) / this.ladderMax * 100;
  }

  tierWidth(i: number): number {
    const t = this.tierRows[i];
    if (!t.from || !t.to || t.to < t.from) return 0;
    return (t.to - t.from + 1) / this.ladderMax * 100;
  }

  tierSpan(i: number): number {
    const t = this.tierRows[i];
    return t.from > 0 && t.to >= t.from ? t.to - t.from + 1 : 0;
  }

  /** Indices of tiers whose day range overlaps another tier. */
  get overlappingTiers(): number[] {
    const rows = this.tierRows;
    const hits = new Set<number>();
    rows.forEach((a, i) => rows.forEach((b, j) => {
      if (i < j && a.from && a.to && b.from && b.to && a.from <= b.to && b.from <= a.to) {
        hits.add(i);
        hits.add(j);
      }
    }));
    return [...hits];
  }

  isOverlapping(i: number): boolean {
    return this.overlappingTiers.includes(i);
  }

  get companyPercent(): number {
    return Math.max(0, Math.round((100 - this.commissionTotal) * 100) / 100);
  }

  get selectedZoneGroupName(): string {
    const id = this.cityForm.get('zoneGroupId')?.value;
    return this.zoneGroups.find(g => Number(g.zoneGroupId) === Number(id))?.name ?? '';
  }

  scrollToSection(id: string): void {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  loadCity(): void {
    if (!this.cityId) return;

    this.isLoading = true;
    this.dispatchService.getCity(this.cityId).subscribe({
      next: ({ city, commission }: { city: CityDto; commission: CityCommission }) => {
        this.cityForm.patchValue({
          deliveryLegCommissionPercent: commission.deliveryLegCommissionPercent,
          returnLegCommissionPercent: commission.returnLegCommissionPercent,
          name: city.name,
          description: city.description ?? null,
          zoneGroupId: city.zoneGroupId ?? null,
          urgentDelivery: city.urgentDelivery ?? 0,
          serviceFees: city.serviceFees ?? 0,
          cancellationFees: city.cancellationFees ?? 0
        });

        while (this.tieredDiscountsFormArray.length !== 0) {
          this.tieredDiscountsFormArray.removeAt(0);
        }

        if (city.tieredDiscounts && city.tieredDiscounts.length > 0) {
          city.tieredDiscounts.forEach(td => {
            this.addTieredDiscount(td.from, td.to, td.discount, td.id);
          });
        }

        this.isLoading = false;
      },
      error: (error: any) => {
        this.errorMessage = this.localeService.translate('cities.failedToLoad');
        this.isLoading = false;
        console.error('Error loading city:', error);
      }
    });
  }

  onSubmit(): void {
    if (this.cityForm.get('name')?.invalid || this.cityForm.get('zoneGroupId')?.invalid) {
      this.cityForm.markAllAsTouched();
      return;
    }

    if (
      this.cityForm.get('deliveryLegCommissionPercent')?.invalid
      || this.cityForm.get('returnLegCommissionPercent')?.invalid
      || this.cityForm.hasError('commissionOver100')
    ) {
      this.cityForm.get('deliveryLegCommissionPercent')?.markAsTouched();
      this.cityForm.get('returnLegCommissionPercent')?.markAsTouched();
      this.errorMessage = this.localeService.translate(
        this.cityForm.hasError('commissionOver100') ? 'cities.commissionSumTooHigh' : 'cities.commissionRange'
      );
      return;
    }

    const invalidTiers: number[] = [];
    this.tieredDiscountsFormArray.controls.forEach((control, index) => {
      const from = control.get('from')?.value ?? 0;
      const to = control.get('to')?.value ?? 0;
      const discount = control.get('discount')?.value ?? 0;

      const hasAnyData = (from && from > 0) || (to && to > 0) || (discount && discount > 0);

      if (hasAnyData) {
        control.markAllAsTouched();
        control.updateValueAndValidity();

        if (control.invalid) {
          invalidTiers.push(index + 1);
        }
      }
    });

    if (invalidTiers.length > 0) {
      this.errorMessage = this.localeService.translate('cities.tierIncomplete', {
        tiers: invalidTiers.join(', ')
      });
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';

    const formValue = this.cityForm.value;

    const tieredDiscounts: TieredDiscountDto[] = [];
    this.tieredDiscountsFormArray.controls.forEach(control => {
      const from = control.get('from')?.value ?? 0;
      const to = control.get('to')?.value ?? 0;
      const discount = control.get('discount')?.value ?? 0;

      if (from && from > 0 && to && to > 0 && discount !== null && discount !== undefined && to > from && discount >= 0 && discount <= 100) {
        const td = new TieredDiscountDto();
        td.id = 0;
        td.cityId = this.isEditMode && this.cityId ? this.cityId : 0;
        td.from = from;
        td.to = to;
        td.discount = discount;
        tieredDiscounts.push(td);
      }
    });

    if (this.isEditMode && this.cityId) {
      const command = new UpdateCityCommand();
      command.cityId = this.cityId;
      command.name = formValue.name;
      command.description = formValue.description || null;
      command.zoneGroupId = formValue.zoneGroupId ? Number(formValue.zoneGroupId) : null;
      command.urgentDelivery = formValue.urgentDelivery ?? null;
      command.serviceFees = formValue.serviceFees ?? null;
      command.cancellationFees = formValue.cancellationFees ?? null;
      command.tieredDiscounts = tieredDiscounts.length > 0 ? tieredDiscounts : null;

      this.dispatchService.updateCity(command, this.commissionValue).subscribe({
        next: () => {
          this.router.navigate(['/main/cities']);
        },
        error: (error: any) => {
          this.errorMessage = this.extractErrorMessage(error) || this.localeService.translate('cities.failedToUpdate');
          this.isSaving = false;
          console.error('Error updating city:', error);
        }
      });
    } else {
      const command = new AddCityCommand();
      command.name = formValue.name;
      command.description = formValue.description || null;
      command.zoneGroupId = formValue.zoneGroupId ? Number(formValue.zoneGroupId) : null;
      command.urgentDelivery = formValue.urgentDelivery ?? null;
      command.serviceFees = formValue.serviceFees ?? null;
      command.cancellationFees = formValue.cancellationFees ?? null;
      command.tieredDiscounts = tieredDiscounts.length > 0 ? tieredDiscounts : null;

      this.dispatchService.addCity(command, this.commissionValue).subscribe({
        next: () => {
          this.router.navigate(['/main/cities']);
        },
        error: (error: any) => {
          this.errorMessage = this.extractErrorMessage(error) || this.localeService.translate('cities.failedToCreate');
          this.isSaving = false;
          console.error('Error adding city:', error);
        }
      });
    }
  }

  onCancel(): void {
    this.router.navigate(['/main/cities']);
  }

  private extractErrorMessage(error: any): string {
    if (error.error) {
      if (error.error.errorMessage) {
        return error.error.errorMessage;
      } else if (error.error.detail) {
        return error.error.detail;
      } else if (error.error.title) {
        return error.error.title;
      } else if (typeof error.error === 'string') {
        return error.error;
      }
    } else if (error.message) {
      return error.message;
    }
    return '';
  }
}
