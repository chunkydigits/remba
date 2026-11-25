import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MaterialModule } from '../../../../core/material.module';
import { EntityTypeSelectComponent } from '../entity-type-select/entity-type-select.component';
import { ListService } from '../../services/list.service';
import {
  LIST_ATTRIBUTE_TYPE_OPTIONS,
  LIST_MEMBER_ROLE_OPTIONS,
  ListAttributeDefinition,
  ListAttributeType,
  ListDetail,
  ListFieldKey,
  ListInvite,
  ListMemberRole,
  ListShareMember,
  ShareGroup,
  UpdateListRequest,
  UpdateListViewSettingsRequest,
  availableFieldsForEntityType,
  defaultVisibleFields,
} from '../../models/list.model';

@Component({
  selector: 'app-list-settings',
  imports: [ReactiveFormsModule, MaterialModule, EntityTypeSelectComponent],
  templateUrl: './list-settings.component.html',
  styleUrl: './list-settings.component.scss',
})
export class ListSettingsComponent implements OnChanges {
  private readonly formBuilder = inject(FormBuilder);
  private readonly listService = inject(ListService);

  @Input({ required: true }) list!: ListDetail;
  @Input() saveSuccess = false;
  @Input() saveError = '';
  @Input() deleteError = '';

  @Output() readonly saveDetails = new EventEmitter<UpdateListRequest>();
  @Output() readonly saveViewSettings = new EventEmitter<UpdateListViewSettingsRequest>();
  @Output() readonly deleteList = new EventEmitter<void>();
  @Output() readonly sharesChanged = new EventEmitter<void>();
  @Output() readonly leaveList = new EventEmitter<void>();

  readonly attributeTypeOptions = LIST_ATTRIBUTE_TYPE_OPTIONS;
  readonly memberRoleOptions = LIST_MEMBER_ROLE_OPTIONS;

  readonly detailsForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', [Validators.maxLength(1000)]],
    referralLink: ['', [Validators.maxLength(2000)]],
    entityType: ['', [Validators.maxLength(40)]],
    visibility: ['private', Validators.required],
  });

  readonly viewForm = this.formBuilder.nonNullable.group({
    viewMode: ['list', Validators.required],
  });

  readonly propertyForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    description: ['', [Validators.maxLength(400)]],
    type: ['text' as ListAttributeType, Validators.required],
  });

  readonly inviteForm = this.formBuilder.nonNullable.group({
    role: ['editor' as ListMemberRole, Validators.required],
  });

  readonly shareForm = this.formBuilder.nonNullable.group({
    usernames: ['', [Validators.maxLength(400)]],
    role: ['editor' as ListMemberRole, Validators.required],
    shareLabel: ['', [Validators.maxLength(80)]],
    shareGroupId: [''],
  });

  readonly groupForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    usernames: ['', [Validators.required, Validators.maxLength(400)]],
  });

  visibleFieldKeys: ListFieldKey[] = ['name'];
  customProperties: ListAttributeDefinition[] = [];
  members: ListShareMember[] = [];
  invites: ListInvite[] = [];
  shareGroups: ShareGroup[] = [];
  shareError = '';
  shareSuccess = '';
  inviteError = '';
  inviteSuccess = '';
  lastCreatedInvite: ListInvite | null = null;
  groupError = '';
  sharingBusy = false;
  selectedTabIndex = 0;
  showPropertyForm = false;
  showUsedInvites = false;

  private detailsBaseline = '';
  private propertiesBaseline = '';
  private displayBaseline = '';

  get isOwner(): boolean {
    return this.list?.isOwned !== false && (this.list?.myRole || 'owner') === 'owner';
  }

  get isSharedMember(): boolean {
    return this.list?.isOwned === false;
  }

  get detailsDirty(): boolean {
    return this.serialiseDetails() !== this.detailsBaseline;
  }

  get propertiesDirty(): boolean {
    return this.serialiseProperties() !== this.propertiesBaseline;
  }

  get displayDirty(): boolean {
    return this.serialiseDisplay() !== this.displayBaseline;
  }

  get activeInvites(): ListInvite[] {
    return this.invites.filter((invite) => !invite.isUsed);
  }

  get usedInvites(): ListInvite[] {
    return this.invites.filter((invite) => invite.isUsed);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['list'] || !this.list) {
      return;
    }

    this.detailsForm.patchValue({
      name: this.list.name,
      description: this.list.description,
      referralLink: this.list.referralLink || '',
      entityType: this.list.entityType || '',
      visibility: this.list.visibility || 'private',
    });

    this.viewForm.patchValue({
      viewMode: this.list.viewSettings.viewMode || 'list',
    });

    const saved = this.list.viewSettings.visibleFields?.filter(Boolean) as ListFieldKey[] | undefined;
    this.visibleFieldKeys =
      saved && saved.length > 0 ? [...saved] : defaultVisibleFields(this.list.entityType);

    this.customProperties = (this.list.attributes || []).map((attribute) => ({ ...attribute }));
    this.members = [...(this.list.members || [])];
    this.showPropertyForm = false;
    this.propertyForm.reset({ name: '', description: '', type: 'text' });
    this.captureBaselines();

    if (this.isOwner) {
      this.loadShareGroups();
      this.loadInvites();
    }
  }

  onCreateAndShareInvite(): void {
    if (!this.isOwner || this.sharingBusy) {
      return;
    }

    this.sharingBusy = true;
    this.inviteError = '';
    this.inviteSuccess = '';
    const role = this.inviteForm.controls.role.value;

    this.listService.createInvite(this.list.id, { role }).subscribe({
      next: (invite) => {
        this.invites = [invite, ...this.invites];
        this.lastCreatedInvite = invite;
        this.sharingBusy = false;
        this.inviteSuccess = `Code ${invite.code} ready to share.`;
        void this.openShareSheet(invite);
      },
      error: (error) => {
        this.sharingBusy = false;
        this.inviteError = error?.error?.message || 'Unable to create invite code.';
      },
    });
  }

  onShareExistingInvite(invite: ListInvite): void {
    if (invite.isUsed) {
      return;
    }

    void this.openShareSheet(invite);
  }

  onCopyInvite(invite: ListInvite): void {
    const text = this.buildShareText(invite);
    if (navigator.clipboard?.writeText) {
      void navigator.clipboard.writeText(text).then(() => {
        this.inviteSuccess = 'Copied share message.';
      });
      return;
    }

    this.inviteSuccess = text;
  }

  onDeleteInvite(invite: ListInvite): void {
    if (!this.isOwner || this.sharingBusy || invite.isUsed) {
      return;
    }

    this.sharingBusy = true;
    this.inviteError = '';
    this.listService.deleteInvite(this.list.id, invite.id).subscribe({
      next: () => {
        this.invites = this.invites.filter((item) => item.id !== invite.id);
        if (this.lastCreatedInvite?.id === invite.id) {
          this.lastCreatedInvite = null;
        }
        this.sharingBusy = false;
      },
      error: (error) => {
        this.sharingBusy = false;
        this.inviteError = error?.error?.message || 'Unable to delete invite code.';
      },
    });
  }

  onShare(): void {
    if (!this.isOwner || this.sharingBusy) {
      return;
    }

    const value = this.shareForm.getRawValue();
    const usernames = this.parseUsernames(value.usernames);
    if (usernames.length === 0 && !value.shareGroupId) {
      this.shareForm.controls.usernames.setErrors({ required: true });
      this.shareForm.markAllAsTouched();
      return;
    }

    this.sharingBusy = true;
    this.shareError = '';
    this.shareSuccess = '';
    this.listService
      .share(this.list.id, {
        usernames,
        shareGroupId: value.shareGroupId || undefined,
        role: value.role,
        shareLabel: value.shareLabel.trim() || undefined,
      })
      .subscribe({
        next: (members) => {
          this.members = members;
          this.sharingBusy = false;
          this.shareSuccess = 'Shared.';
          this.shareForm.patchValue({ usernames: '', shareGroupId: '' });
          this.sharesChanged.emit();
        },
        error: (error) => {
          this.sharingBusy = false;
          this.shareError = error?.error?.message || 'Unable to share list.';
        },
      });
  }

  onMemberRoleChange(member: ListShareMember, role: string): void {
    if (!this.isOwner || this.sharingBusy || member.role === role) {
      return;
    }

    this.sharingBusy = true;
    this.shareError = '';
    this.listService.updateMemberRole(this.list.id, member.userId, { role }).subscribe({
      next: (members) => {
        this.members = members;
        this.sharingBusy = false;
        this.sharesChanged.emit();
      },
      error: (error) => {
        this.sharingBusy = false;
        this.shareError = error?.error?.message || 'Unable to update access level.';
      },
    });
  }

  onUnshare(member: ListShareMember): void {
    if (!this.isOwner || this.sharingBusy) {
      return;
    }

    this.sharingBusy = true;
    this.shareError = '';
    this.listService.unshare(this.list.id, member.userId).subscribe({
      next: () => {
        this.members = this.members.filter((item) => item.userId !== member.userId);
        this.sharingBusy = false;
        this.sharesChanged.emit();
      },
      error: (error) => {
        this.sharingBusy = false;
        this.shareError = error?.error?.message || 'Unable to remove share.';
      },
    });
  }

  onCreateGroup(): void {
    if (!this.isOwner || this.groupForm.invalid || this.sharingBusy) {
      this.groupForm.markAllAsTouched();
      return;
    }

    const value = this.groupForm.getRawValue();
    const usernames = this.parseUsernames(value.usernames);
    if (usernames.length === 0) {
      this.groupForm.controls.usernames.setErrors({ required: true });
      return;
    }

    this.sharingBusy = true;
    this.groupError = '';
    this.listService
      .createShareGroup({ name: value.name.trim(), usernames })
      .subscribe({
        next: (group) => {
          this.shareGroups = [...this.shareGroups, group].sort((a, b) =>
            a.name.localeCompare(b.name),
          );
          this.sharingBusy = false;
          this.groupForm.reset({ name: '', usernames: '' });
          this.shareForm.patchValue({ shareGroupId: group.id, shareLabel: group.name });
        },
        error: (error) => {
          this.sharingBusy = false;
          this.groupError = error?.error?.message || 'Unable to create group.';
        },
      });
  }

  onDeleteGroup(group: ShareGroup): void {
    if (!this.isOwner || this.sharingBusy) {
      return;
    }

    this.sharingBusy = true;
    this.groupError = '';
    this.listService.deleteShareGroup(group.id).subscribe({
      next: () => {
        this.shareGroups = this.shareGroups.filter((item) => item.id !== group.id);
        if (this.shareForm.controls.shareGroupId.value === group.id) {
          this.shareForm.patchValue({ shareGroupId: '' });
        }
        this.sharingBusy = false;
      },
      error: (error) => {
        this.sharingBusy = false;
        this.groupError = error?.error?.message || 'Unable to delete group.';
      },
    });
  }

  onLeave(): void {
    this.leaveList.emit();
  }

  onShowPropertyForm(): void {
    this.showPropertyForm = true;
  }

  onCancelPropertyForm(): void {
    this.showPropertyForm = false;
    this.propertyForm.reset({ name: '', description: '', type: 'text' });
  }

  private loadShareGroups(): void {
    this.listService.getShareGroups().subscribe({
      next: (groups) => {
        this.shareGroups = groups;
      },
      error: () => {
        this.shareGroups = [];
      },
    });
  }

  private loadInvites(): void {
    this.listService.getInvites(this.list.id).subscribe({
      next: (invites) => {
        this.invites = invites;
      },
      error: () => {
        this.invites = [];
      },
    });
  }

  private async openShareSheet(invite: ListInvite): Promise<void> {
    const text = this.buildShareText(invite);
    const shareData: ShareData = {
      title: `Join “${this.list.name}” on Remba`,
      text,
      url: this.buildJoinUrl(invite.code),
    };

    try {
      if (navigator.share && (!navigator.canShare || navigator.canShare(shareData))) {
        await navigator.share(shareData);
        return;
      }
    } catch {
      // User cancelled or share unavailable — fall through to SMS/copy.
    }

    const smsBody = encodeURIComponent(text);
    window.location.href = `sms:?&body=${smsBody}`;
  }

  private buildShareText(invite: ListInvite): string {
    const joinUrl = this.buildJoinUrl(invite.code);
    return `Join my Remba list “${this.list.name}” with code ${invite.code}. Sign in, then open ${joinUrl}`;
  }

  private buildJoinUrl(code: string): string {
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    return `${origin}/lists/join?code=${encodeURIComponent(code)}`;
  }

  private parseUsernames(value: string): string[] {
    return value
      .split(/[,;\s]+/)
      .map((item) => item.trim().replace(/^@/, ''))
      .filter(Boolean);
  }

  availableFields() {
    const builtIn = availableFieldsForEntityType(
      this.detailsForm.controls.entityType.value || this.list.entityType,
    );
    const custom = this.customProperties
      .filter((property) => !!property.key?.trim())
      .map((property) => ({
        key: property.key as ListFieldKey,
        label: property.name,
      }));
    return [...builtIn, ...custom];
  }

  isFieldVisible(key: ListFieldKey | string): boolean {
    return this.visibleFieldKeys.includes(key as ListFieldKey);
  }

  onToggleField(key: ListFieldKey | string, checked: boolean): void {
    if (key === 'name') {
      return;
    }

    const typedKey = key as ListFieldKey;
    if (checked && !this.visibleFieldKeys.includes(typedKey)) {
      this.visibleFieldKeys = [...this.visibleFieldKeys, typedKey];
      return;
    }

    if (!checked) {
      this.visibleFieldKeys = this.visibleFieldKeys.filter((field) => field !== typedKey);
    }
  }

  attributeTypeLabel(type: string): string {
    return this.attributeTypeOptions.find((option) => option.value === type)?.label || type;
  }

  roleLabel(role: string): string {
    return this.memberRoleOptions.find((option) => option.value === role)?.label || role;
  }

  onAddProperty(): void {
    if (this.propertyForm.invalid) {
      this.propertyForm.markAllAsTouched();
      return;
    }

    const value = this.propertyForm.getRawValue();
    const name = value.name.trim();
    const key = this.slugify(name);
    if (!key) {
      this.propertyForm.controls.name.setErrors({ invalidKey: true });
      return;
    }

    if (this.customProperties.some((property) => property.key.toLowerCase() === key.toLowerCase())) {
      this.propertyForm.controls.name.setErrors({ duplicate: true });
      return;
    }

    this.customProperties = [
      ...this.customProperties,
      {
        key,
        name,
        description: value.description.trim() || undefined,
        type: value.type,
        scope: 'entry',
      },
    ];

    if (!this.visibleFieldKeys.includes(key as ListFieldKey)) {
      this.visibleFieldKeys = [...this.visibleFieldKeys, key as ListFieldKey];
    }

    this.propertyForm.reset({
      name: '',
      description: '',
      type: 'text',
    });
    this.showPropertyForm = false;
  }

  onRemoveProperty(key: string): void {
    this.customProperties = this.customProperties.filter((property) => property.key !== key);
    this.visibleFieldKeys = this.visibleFieldKeys.filter((field) => field !== key);
  }

  onSaveDetails(): void {
    if (this.detailsForm.invalid || !this.detailsDirty) {
      this.detailsForm.markAllAsTouched();
      return;
    }

    const value = this.detailsForm.getRawValue();
    this.saveDetails.emit({
      name: value.name,
      description: value.description,
      referralLink: value.referralLink.trim(),
      entityType: value.entityType || undefined,
      visibility: value.visibility,
    });
  }

  onSaveProperties(): void {
    if (!this.propertiesDirty) {
      return;
    }

    for (const property of this.customProperties) {
      if (property.key && !this.visibleFieldKeys.includes(property.key as ListFieldKey)) {
        this.visibleFieldKeys = [...this.visibleFieldKeys, property.key as ListFieldKey];
      }
    }

    this.saveDetails.emit({
      attributes: this.customProperties.map((property) => ({
        key: property.key,
        name: property.name,
        description: property.description || undefined,
        type: property.type,
      })),
    });

    // Keep display settings in sync so new properties appear on the list immediately.
    this.onSaveViewSettings();
  }

  onSaveViewSettings(): void {
    if (!this.displayDirty && !this.propertiesDirty) {
      return;
    }

    const value = this.viewForm.getRawValue();
    const fields = this.visibleFieldKeys.includes('name')
      ? this.visibleFieldKeys
      : (['name', ...this.visibleFieldKeys] as ListFieldKey[]);

    this.saveViewSettings.emit({
      viewMode: value.viewMode,
      visibleFields: fields,
    });
  }

  onDelete(): void {
    this.deleteList.emit();
  }

  private captureBaselines(): void {
    this.detailsBaseline = this.serialiseDetails();
    this.propertiesBaseline = this.serialiseProperties();
    this.displayBaseline = this.serialiseDisplay();
  }

  private serialiseDetails(): string {
    const value = this.detailsForm.getRawValue();
    return JSON.stringify({
      name: value.name,
      description: value.description,
      referralLink: value.referralLink,
      entityType: value.entityType,
      visibility: value.visibility,
    });
  }

  private serialiseProperties(): string {
    return JSON.stringify(this.customProperties);
  }

  private serialiseDisplay(): string {
    return JSON.stringify({
      viewMode: this.viewForm.controls.viewMode.value,
      visibleFields: this.visibleFieldKeys,
    });
  }

  private slugify(value: string): string {
    return value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/-+/g, '-')
      .replace(/^-|-$/g, '');
  }
}
