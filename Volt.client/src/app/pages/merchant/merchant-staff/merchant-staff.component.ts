import { Component, OnInit, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  CreateMerchantStaffCommand,
  MerchantStaffClient,
  MerchantStaffDto,
  SubRoleLookupDto,
  UpdateMerchantStaffCommand
} from '../../../core/services/clientAPI';
import { AuthService } from '../../../core/services/auth.service';
import { LocaleService } from '../../../core/services/locale.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  MultiSelectComponent,
  MultiSelectOption
} from '../../../shared/components/multi-select/multi-select.component';
import { HasPermissionDirective } from '../../../shared/directives/has-permission.directive';

type StaffAction = 'delete' | 'activate' | 'deactivate';

/** Owner and staff logins of the signed-in merchant; staff get a sub-role defined by the admin. */
@Component({
  selector: 'app-merchant-staff',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, TranslatePipe, ConfirmDialogComponent, MultiSelectComponent, HasPermissionDirective],
  templateUrl: './merchant-staff.component.html',
  styleUrls: ['../../users/users.component.css', '../../../shared/styles/entity-form.css']
})
export class MerchantStaffComponent implements OnInit {
  private staffClient = inject(MerchantStaffClient);
  private authService = inject(AuthService);
  private localeService = inject(LocaleService);
  private fb = inject(FormBuilder);

  staff: MerchantStaffDto[] = [];
  subRoles: SubRoleLookupDto[] = [];
  isLoading = false;
  errorMessage = '';
  successMessage = '';

  showModal = false;
  editing: MerchantStaffDto | null = null;
  isSubmitting = false;
  staffForm: FormGroup;

  showConfirmDialog = false;
  confirmDialogLoading = false;
  pending: { action: StaffAction; staff: MerchantStaffDto } | null = null;

  constructor() {
    this.staffForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.minLength(2)]],
      userName: ['', [Validators.required, Validators.minLength(3)]],
      phoneNumber: ['', [Validators.required]],
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8)]],
      subRoleId: [null, [Validators.required]]
    });
  }

  ngOnInit(): void {
    this.loadStaff();
    if (this.authService.hasAnyPermission(['Merchant.Staff.Create', 'Merchant.Staff.Edit'])) {
      this.staffClient.getAvailableSubRoles().subscribe({ next: roles => (this.subRoles = roles) });
    }
  }

  get subRoleOptions(): MultiSelectOption[] {
    const ar = this.localeService.locale() === 'ar';
    return this.subRoles.map(r => ({ value: r.subRoleId, label: ar && r.nameAr ? r.nameAr : r.name }));
  }

  subRoleLabel(member: MerchantStaffDto): string {
    return this.localeService.locale() === 'ar' && member.subRoleNameAr ? member.subRoleNameAr : member.subRoleName;
  }

  /** The owner and the signed-in user cannot be changed from this page. */
  canChange(member: MerchantStaffDto): boolean {
    return !member.isOwner && member.userId !== this.authService.getUserData()?.userId;
  }

  loadStaff(): void {
    this.isLoading = true;
    this.staffClient.getMyStaff().subscribe({
      next: staff => {
        this.staff = staff;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = this.localeService.translate('common.failedToLoad');
        this.isLoading = false;
      }
    });
  }

  onAddNew(): void {
    this.editing = null;
    this.staffForm.reset({ subRoleId: null });
    this.staffForm.get('userName')!.enable();
    this.setPasswordRequired(true);
    this.showModal = true;
  }

  onEdit(member: MerchantStaffDto): void {
    this.editing = member;
    this.staffForm.reset({
      fullName: member.fullName,
      userName: member.userName,
      phoneNumber: member.phoneNumber ?? '',
      email: member.email ?? '',
      password: '',
      subRoleId: member.subRoleId
    });
    this.staffForm.get('userName')!.disable();
    this.setPasswordRequired(false);
    this.showModal = true;
  }

  private setPasswordRequired(required: boolean): void {
    const password = this.staffForm.get('password')!;
    password.setValidators(required ? [Validators.required, Validators.minLength(8)] : [Validators.minLength(8)]);
    password.updateValueAndValidity();
  }

  hasFieldError(name: string): boolean {
    const field = this.staffForm.get(name);
    return !!(field && field.invalid && (field.dirty || field.touched));
  }

  onSubmit(): void {
    if (this.staffForm.invalid) {
      this.staffForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    const value = this.staffForm.getRawValue();
    const request$: Observable<unknown> = this.editing
      ? this.staffClient.updateStaff(this.editing.merchantUserId, UpdateMerchantStaffCommand.fromJS({
          merchantUserId: this.editing.merchantUserId,
          fullName: value.fullName,
          phoneNumber: value.phoneNumber,
          email: value.email,
          subRoleId: value.subRoleId,
          password: value.password || null
        }))
      : this.staffClient.createStaff(CreateMerchantStaffCommand.fromJS(value));

    request$.subscribe({
      next: () => {
        this.isSubmitting = false;
        this.showModal = false;
        this.showSuccess(this.editing ? 'common.updatedSuccessfully' : 'common.createdSuccessfully');
        this.editing = null;
        this.loadStaff();
      },
      error: (error: any) => {
        this.isSubmitting = false;
        this.showError(error?.errorMessage || this.localeService.translate('common.failedToSave'));
      }
    });
  }

  onCloseModal(): void {
    this.showModal = false;
    this.editing = null;
    this.isSubmitting = false;
  }

  confirm(action: StaffAction, member: MerchantStaffDto): void {
    this.pending = { action, staff: member };
    this.showConfirmDialog = true;
  }

  get confirmTitle(): string {
    return this.localeService.translate(`merchant.staff.${this.pending?.action ?? 'delete'}Title`);
  }

  get confirmMessage(): string {
    return this.localeService.translate(`merchant.staff.${this.pending?.action ?? 'delete'}Confirm`, {
      name: this.pending?.staff.fullName ?? ''
    });
  }

  onCancelConfirm(): void {
    this.showConfirmDialog = false;
    this.confirmDialogLoading = false;
    this.pending = null;
  }

  onConfirm(): void {
    if (!this.pending) return;
    const { action, staff } = this.pending;
    this.confirmDialogLoading = true;
    const request$ =
      action === 'delete'
        ? this.staffClient.deleteStaff(staff.merchantUserId)
        : action === 'activate'
          ? this.staffClient.activateStaff(staff.merchantUserId)
          : this.staffClient.deactivateStaff(staff.merchantUserId);

    request$.subscribe({
      next: () => {
        this.onCancelConfirm();
        this.showSuccess(action === 'delete' ? 'common.deletedSuccessfully' : 'common.updatedSuccessfully');
        this.loadStaff();
      },
      error: (error: any) => {
        this.onCancelConfirm();
        this.showError(error?.errorMessage || this.localeService.translate('common.failedToSave'));
      }
    });
  }

  private showSuccess(key: string): void {
    this.successMessage = this.localeService.translate(key);
    this.errorMessage = '';
    setTimeout(() => (this.successMessage = ''), 5000);
  }

  private showError(message: string): void {
    this.errorMessage = message;
    this.successMessage = '';
    setTimeout(() => (this.errorMessage = ''), 5000);
  }
}
