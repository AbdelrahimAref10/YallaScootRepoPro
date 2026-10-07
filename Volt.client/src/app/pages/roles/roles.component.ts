import { Component, OnInit, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import {
  CreateSubRoleCommand,
  PermissionModuleDto,
  SubRoleClient,
  SubRoleDetailDto,
  SubRoleDto,
  UpdateSubRoleCommand
} from '../../core/services/clientAPI';
import { LocaleService } from '../../core/services/locale.service';
import { AppRole } from '../../core/models/app-role';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { HasPermissionDirective } from '../../shared/directives/has-permission.directive';

/** Column order of the permission matrix. */
const ACTIONS = ['View', 'Create', 'Edit', 'Delete'] as const;
type PanelScope = AppRole.SuperAdmin | AppRole.Merchant;

/**
 * Sub-roles of the admin and merchant panels. Each sub-role gets a set of
 * module × action permissions; system roles (Admin, Merchant Owner) have full access and are read-only.
 */
@Component({
  selector: 'app-roles',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule, TranslatePipe, ConfirmDialogComponent, HasPermissionDirective],
  templateUrl: './roles.component.html',
  styleUrl: './roles.component.css'
})
export class RolesComponent implements OnInit {
  private subRoleClient = inject(SubRoleClient);
  private fb = inject(FormBuilder);
  private localeService = inject(LocaleService);

  readonly AppRole = AppRole;
  readonly actions = ACTIONS;
  readonly scopes: { value: PanelScope; labelKey: string }[] = [
    { value: AppRole.SuperAdmin, labelKey: 'roles.scopeAdmin' },
    { value: AppRole.Merchant, labelKey: 'roles.scopeMerchant' }
  ];

  scope: PanelScope = AppRole.SuperAdmin;
  roles: SubRoleDto[] = [];
  isLoading = false;
  errorMessage = '';
  successMessage = '';

  /** Permission matrix of the current scope, cached per scope. */
  private matrixByScope = new Map<PanelScope, PermissionModuleDto[]>();
  modules: PermissionModuleDto[] = [];
  selected = new Set<string>();

  showModal = false;
  roleForm: FormGroup;
  editingRole: SubRoleDetailDto | null = null;
  isSubmitting = false;
  isLoadingRole = false;

  showConfirmDialog = false;
  confirmDialogTitle = '';
  confirmDialogMessage = '';
  confirmDialogLoading = false;
  pendingDeleteId: number | null = null;

  constructor() {
    this.roleForm = this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      nameAr: ['', [Validators.maxLength(100)]],
      isActive: [true]
    });
  }

  ngOnInit(): void {
    this.loadRoles();
  }

  get isReadOnly(): boolean {
    return !!this.editingRole?.isSystem;
  }

  get isEditing(): boolean {
    return !!this.editingRole;
  }

  roleName(role: { name: string; nameAr?: string | null }): string {
    return this.localeService.locale() === 'ar' && role.nameAr ? role.nameAr : role.name;
  }

  moduleLabel(module: string): string {
    return this.localeService.translate(`modules.${module}`);
  }

  onScopeChange(scope: PanelScope): void {
    if (this.scope === scope) return;
    this.scope = scope;
    this.loadRoles();
  }

  loadRoles(): void {
    this.isLoading = true;
    this.errorMessage = '';
    this.subRoleClient.getAll(this.scope, undefined).subscribe({
      next: roles => {
        this.roles = roles;
        this.isLoading = false;
      },
      error: () => {
        this.errorMessage = this.localeService.translate('common.failedToLoad');
        this.isLoading = false;
      }
    });
  }

  onAddNew(): void {
    this.editingRole = null;
    this.roleForm.reset({ name: '', nameAr: '', isActive: true });
    this.roleForm.enable();
    this.selected = new Set();
    this.openModal();
  }

  onEdit(role: SubRoleDto): void {
    this.isLoadingRole = true;
    this.subRoleClient.getById(role.subRoleId).subscribe({
      next: detail => {
        this.editingRole = detail;
        this.roleForm.reset({ name: detail.name, nameAr: detail.nameAr ?? '', isActive: detail.isActive });
        if (detail.isSystem) {
          this.roleForm.disable();
        } else {
          this.roleForm.enable();
        }
        this.selected = new Set(detail.permissions ?? []);
        this.isLoadingRole = false;
        this.openModal();
      },
      error: () => {
        this.isLoadingRole = false;
        this.showErrorMessage(this.localeService.translate('common.failedToLoad'));
      }
    });
  }

  private openModal(): void {
    const cached = this.matrixByScope.get(this.scope);
    if (cached) {
      this.modules = cached;
      this.showModal = true;
      return;
    }
    const scope = this.scope;
    this.subRoleClient.getPermissions(scope).subscribe({
      next: modules => {
        this.matrixByScope.set(scope, modules);
        this.modules = modules;
        this.showModal = true;
      },
      error: () => this.showErrorMessage(this.localeService.translate('common.failedToLoad'))
    });
  }

  /** Permission name for a module/action cell, or null when the module has no such action. */
  cell(module: PermissionModuleDto, action: string): string | null {
    return module.permissions.find(p => p.action === action)?.name ?? null;
  }

  isChecked(name: string | null): boolean {
    return !!name && this.selected.has(name);
  }

  toggle(module: PermissionModuleDto, action: string): void {
    const name = this.cell(module, action);
    if (!name || this.isReadOnly) return;

    if (this.selected.has(name)) {
      this.selected.delete(name);
      // Without View the other actions of the module are useless.
      if (action === 'View') {
        module.permissions.forEach(p => this.selected.delete(p.name));
      }
    } else {
      this.selected.add(name);
      // Create / Edit / Delete need the page itself.
      const view = this.cell(module, 'View');
      if (view) this.selected.add(view);
    }
  }

  isModuleFull(module: PermissionModuleDto): boolean {
    return module.permissions.every(p => this.selected.has(p.name));
  }

  toggleModule(module: PermissionModuleDto): void {
    if (this.isReadOnly) return;
    const full = this.isModuleFull(module);
    module.permissions.forEach(p => (full ? this.selected.delete(p.name) : this.selected.add(p.name)));
  }

  get isAllSelected(): boolean {
    return this.modules.length > 0 && this.modules.every(m => this.isModuleFull(m));
  }

  toggleAll(): void {
    if (this.isReadOnly) return;
    const all = this.isAllSelected;
    this.modules.forEach(m => m.permissions.forEach(p => (all ? this.selected.delete(p.name) : this.selected.add(p.name))));
  }

  onSubmit(): void {
    if (this.isReadOnly) return;
    if (this.roleForm.invalid) {
      this.roleForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    const { name, nameAr, isActive } = this.roleForm.value;
    const permissions = Array.from(this.selected);

    const request$: Observable<unknown> = this.editingRole
      ? this.subRoleClient.update(this.editingRole.subRoleId, UpdateSubRoleCommand.fromJS({
          subRoleId: this.editingRole.subRoleId, name, nameAr: nameAr || null, isActive, permissions
        }))
      : this.subRoleClient.create(CreateSubRoleCommand.fromJS({ name, nameAr: nameAr || null, scope: this.scope, permissions }));

    request$.subscribe({
      next: () => {
        this.isSubmitting = false;
        this.showModal = false;
        this.showSuccessMessage(this.localeService.translate(this.editingRole ? 'common.updatedSuccessfully' : 'common.createdSuccessfully'));
        this.editingRole = null;
        this.loadRoles();
      },
      error: (error: any) => {
        this.isSubmitting = false;
        this.showErrorMessage(this.apiError(error, this.editingRole ? 'roles.failedToUpdate' : 'roles.failedToCreate'));
      }
    });
  }

  onCloseModal(): void {
    this.showModal = false;
    this.editingRole = null;
    this.isSubmitting = false;
  }

  onDelete(role: SubRoleDto): void {
    this.pendingDeleteId = role.subRoleId;
    this.confirmDialogTitle = this.localeService.translate('roles.deleteTitle');
    this.confirmDialogMessage = this.localeService.translate('roles.deleteMessage', { name: this.roleName(role) });
    this.showConfirmDialog = true;
  }

  onCancelDelete(): void {
    this.showConfirmDialog = false;
    this.pendingDeleteId = null;
    this.confirmDialogLoading = false;
  }

  onConfirmDelete(): void {
    if (this.pendingDeleteId == null) return;
    this.confirmDialogLoading = true;
    this.subRoleClient.delete(this.pendingDeleteId).subscribe({
      next: () => {
        this.onCancelDelete();
        this.showSuccessMessage(this.localeService.translate('common.deletedSuccessfully'));
        this.loadRoles();
      },
      error: (error: any) => {
        this.onCancelDelete();
        this.showErrorMessage(this.apiError(error, 'roles.failedToDelete'));
      }
    });
  }

  private apiError(error: any, fallbackKey: string): string {
    return error?.errorMessage || error?.detail || error?.title || this.localeService.translate(fallbackKey);
  }

  showSuccessMessage(message: string): void {
    this.successMessage = message;
    this.errorMessage = '';
    setTimeout(() => (this.successMessage = ''), 5000);
  }

  showErrorMessage(message: string): void {
    this.errorMessage = message;
    this.successMessage = '';
    setTimeout(() => (this.errorMessage = ''), 5000);
  }
}
