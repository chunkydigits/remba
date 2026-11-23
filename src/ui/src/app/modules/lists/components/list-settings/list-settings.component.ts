import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MaterialModule } from '../../../../core/material.module';
import { EntityTypeSelectComponent } from '../entity-type-select/entity-type-select.component';
import {
  LIST_ATTRIBUTE_TYPE_OPTIONS,
  ListAttributeDefinition,
  ListAttributeType,
  ListDetail,
  ListFieldKey,
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

  @Input({ required: true }) list!: ListDetail;
  @Input() saveSuccess = false;
  @Input() saveError = '';
  @Input() deleteError = '';

  @Output() readonly saveDetails = new EventEmitter<UpdateListRequest>();
  @Output() readonly saveViewSettings = new EventEmitter<UpdateListViewSettingsRequest>();
  @Output() readonly deleteList = new EventEmitter<void>();

  readonly attributeTypeOptions = LIST_ATTRIBUTE_TYPE_OPTIONS;

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

  visibleFieldKeys: ListFieldKey[] = ['name'];
  customProperties: ListAttributeDefinition[] = [];

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
  }

  onRemoveProperty(key: string): void {
    this.customProperties = this.customProperties.filter((property) => property.key !== key);
    this.visibleFieldKeys = this.visibleFieldKeys.filter((field) => field !== key);
  }

  onSaveDetails(): void {
    if (this.detailsForm.invalid) {
      this.detailsForm.markAllAsTouched();
      return;
    }

    const value = this.detailsForm.getRawValue();
    for (const property of this.customProperties) {
      if (property.key && !this.visibleFieldKeys.includes(property.key as ListFieldKey)) {
        this.visibleFieldKeys = [...this.visibleFieldKeys, property.key as ListFieldKey];
      }
    }

    this.saveDetails.emit({
      name: value.name,
      description: value.description,
      referralLink: value.referralLink.trim(),
      entityType: value.entityType || undefined,
      visibility: value.visibility,
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

  private slugify(value: string): string {
    return value
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/-+/g, '-')
      .replace(/^-|-$/g, '');
  }
}
