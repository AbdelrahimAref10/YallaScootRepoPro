import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import {
  AdminCreateDeliveryCommand,
  AdminDeliveryClient,
  AdminUpdateDeliveryCommand,
  CityDto,
  PagedResultOfCityDto,
  ZoneLookupDto
} from '../../../core/services/clientAPI';
import { LocaleService } from '../../../core/services/locale.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import {
  MultiSelectComponent,
  MultiSelectOption
} from '../../../shared/components/multi-select/multi-select.component';
import { memo } from '../../../shared/utils/memo';
import { LookupService } from '../../../core/services/lookup.service';

@Component({
  selector: 'app-delivery-form',
  standalone: true,
  imports: [ReactiveFormsModule, RouterModule, TranslatePipe, MultiSelectComponent],
  templateUrl: './delivery-form.component.html',
  styleUrls: ['./delivery-form.component.css', '../../../shared/styles/entity-form.css', '../../../shared/styles/record-form.css']
})
export class DeliveryFormComponent implements OnInit {
  private readonly lookups = inject(LookupService);
  private readonly cityOptionsMemo = memo<MultiSelectOption[]>();
  private readonly zoneOptionsMemo = memo<MultiSelectOption[]>();

  @ViewChild('personalImageInput', { static: false }) personalImageInputRef?: ElementRef<HTMLInputElement>;

  private readonly localeService = inject(LocaleService);
  private readonly deliveryClient = inject(AdminDeliveryClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);

  deliveryForm!: FormGroup;
  isEditMode = false;
  deliveryId: number | null = null;
  isLoading = false;
  isSaving = false;
  errorMessage = '';
  personalImagePreview: string | null = null;
  existingPersonalImage: string | null = null;
  cities: CityDto[] = [];
  zones: ZoneLookupDto[] = [];

  /** First letter of the name for the summary avatar. */
  get initial(): string {
    const name = String(this.deliveryForm?.get('fullName')?.value || '').trim();
    return name ? name.charAt(0).toUpperCase() : '?';
  }

  /** Share of required fields already valid, for the summary meter. */
  get completion(): number {
    const required = ['fullName', 'userName', 'mobileNumber', 'email', 'cityId', 'zoneId'];
    if (!this.isEditMode) required.push('password');
    const done = required.filter(name => {
      const control = this.deliveryForm?.get(name);
      return !!control && control.valid && control.value !== null && control.value !== '';
    }).length;
    return Math.round((done / required.length) * 100);
  }

  optionLabel(options: MultiSelectOption[], value: unknown): string {
    if (value === null || value === undefined || value === '') return '—';
    return options.find(o => o.value === value || String(o.value) === String(value))?.label ?? '—';
  }

  get cityOptions(): MultiSelectOption[] {
    return this.cityOptionsMemo([this.cities], () =>
      this.cities.map(city => ({
        value: city.cityId,
        label: city.name
      })));
  }

  get zoneOptions(): MultiSelectOption[] {
    return this.zoneOptionsMemo([this.zones], () =>
      this.zones.map(zone => ({
        value: zone.zoneId,
        label: zone.name
      })));
  }

  ngOnInit(): void {
    this.deliveryForm = this.fb.group({
      userName: ['', [Validators.required, Validators.minLength(3)]],
      fullName: ['', [Validators.required, Validators.minLength(2)]],
      mobileNumber: ['', [Validators.required, Validators.pattern(/^[0-9]+$/)]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.minLength(6)]],
      cityId: [null, [Validators.required]],
      zoneId: [null, [Validators.required]],
      personalImage: [''],
      isActive: [true]
    });

    this.loadCities();
    this.deliveryForm.get('cityId')?.valueChanges.subscribe((cityId: number | null) => {
      this.loadZones(cityId);
    });

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam) {
      this.isEditMode = true;
      this.deliveryId = Number(idParam);
      this.deliveryForm.get('mobileNumber')?.disable();
      this.deliveryForm.get('password')?.clearValidators();
      this.deliveryForm.get('password')?.updateValueAndValidity();
      this.loadDelivery(this.deliveryId);
    } else {
      this.deliveryForm.get('password')?.setValidators([Validators.required, Validators.minLength(6)]);
      this.deliveryForm.get('password')?.updateValueAndValidity();
    }
  }

  loadCities(): void {
    this.lookups.activeCities().subscribe({
      next: (result: PagedResultOfCityDto) => {
        this.cities = result.items || [];
      },
      error: () => {
        this.errorMessage = this.localeService.translate('deliveries.loadFailed');
      }
    });
  }

  loadZones(cityId: number | null, preferredZoneId: number | null = null): void {
    if (!cityId) {
      this.zones = [];
      this.deliveryForm.patchValue({ zoneId: null });
      return;
    }

    this.lookups.zonesByCity(cityId).subscribe({
      next: (zones) => {
        this.zones = zones || [];
        const keep = preferredZoneId ?? this.deliveryForm.get('zoneId')?.value;
        const next = this.zones.some(z => z.zoneId === keep) ? keep : null;
        this.deliveryForm.patchValue({ zoneId: next });
      },
      error: () => {
        this.zones = [];
        this.deliveryForm.patchValue({ zoneId: null });
      }
    });
  }

  loadDelivery(id: number): void {
    this.isLoading = true;
    this.deliveryClient.getById(id).subscribe({
      next: (delivery) => {
        this.existingPersonalImage = delivery.personalImage || null;
        this.personalImagePreview = delivery.personalImage || null;
        this.deliveryForm.patchValue({
          userName: delivery.userName || '',
          fullName: delivery.fullName,
          mobileNumber: delivery.mobileNumber,
          email: delivery.email || '',
          cityId: delivery.cityId || null,
          zoneId: delivery.zoneId || null,
          isActive: delivery.isActive,
          personalImage: ''
        }, { emitEvent: false });
        this.loadZones(delivery.cityId || null, delivery.zoneId || null);
        this.isLoading = false;
      },
      error: (error: any) => {
        this.isLoading = false;
        this.errorMessage =
          error?.errorMessage ||
          error?.error?.errorMessage ||
          this.localeService.translate('deliveries.loadFailed');
      }
    });
  }

  onPersonalImageSelect(): void {
    const input = this.personalImageInputRef?.nativeElement;
    const file = input?.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => {
      const result = String(reader.result || '');
      this.personalImagePreview = result;
      this.deliveryForm.patchValue({ personalImage: result });
    };
    reader.readAsDataURL(file);
  }

  removePersonalImage(): void {
    this.personalImagePreview = null;
    this.existingPersonalImage = null;
    this.deliveryForm.patchValue({ personalImage: '' });
    if (this.personalImageInputRef?.nativeElement) {
      this.personalImageInputRef.nativeElement.value = '';
    }
  }

  onCancel(): void {
    this.router.navigate(['/main/deliveries']);
  }

  onSubmit(): void {
    if (this.deliveryForm.invalid) {
      this.deliveryForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';
    const value = this.deliveryForm.getRawValue();
    const cityId = Number(value.cityId);

    if (this.isEditMode && this.deliveryId) {
      const command = new AdminUpdateDeliveryCommand();
      command.deliveryId = this.deliveryId;
      command.userName = value.userName;
      command.fullName = value.fullName;
      command.email = value.email;
      command.cityId = cityId;
      command.zoneId = Number(value.zoneId);
      command.personalImage = value.personalImage || this.existingPersonalImage || null;
      command.isActive = !!value.isActive;
      command.password = value.password || null;

      this.deliveryClient.update(this.deliveryId, command).subscribe({
        next: () => this.router.navigate(['/main/deliveries']),
        error: (error: any) => {
          this.isSaving = false;
          this.errorMessage =
            error?.errorMessage ||
            error?.error?.errorMessage ||
            this.localeService.translate('deliveries.updateFailed');
        }
      });
      return;
    }

    const create = new AdminCreateDeliveryCommand();
    create.userName = value.userName;
    create.fullName = value.fullName;
    create.mobileNumber = value.mobileNumber;
    create.email = value.email;
    create.password = value.password;
    create.cityId = cityId;
    create.zoneId = Number(value.zoneId);
    create.personalImage = value.personalImage || null;
    create.isActive = !!value.isActive;

    this.deliveryClient.create(create).subscribe({
      next: () => this.router.navigate(['/main/deliveries']),
      error: (error: any) => {
        this.isSaving = false;
        this.errorMessage =
          error?.errorMessage ||
          error?.error?.errorMessage ||
          this.localeService.translate('deliveries.createFailed');
      }
    });
  }
}
