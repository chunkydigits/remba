import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MaterialModule } from '../../../../core/material.module';
import { EntityTypeSelectComponent } from '../entity-type-select/entity-type-select.component';
import {
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

  readonly detailsForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', [Validators.maxLength(1000)]],
    entityType: ['', [Validators.maxLength(40)]],
    visibility: ['private', Validators.required],
  });

  readonly viewForm = this.formBuilder.nonNullable.group({
    viewMode: ['list', Validators.required],
  });

  visibleFieldKeys: ListFieldKey[] = ['name'];

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['list'] || !this.list) {
      return;
    }

    this.detailsForm.patchValue({
      name: this.list.name,
      description: this.list.description,
      entityType: this.list.entityType || '',
      visibility: this.list.visibility || 'private',
    });

    this.viewForm.patchValue({
      viewMode: this.list.viewSettings.viewMode || 'list',
    });

    const saved = this.list.viewSettings.visibleFields?.filter(Boolean) as ListFieldKey[] | undefined;
    this.visibleFieldKeys =
      saved && saved.length > 0 ? [...saved] : defaultVisibleFields(this.list.entityType);
  }

  availableFields() {
    return availableFieldsForEntityType(this.detailsForm.controls.entityType.value || this.list.entityType);
  }

  isFieldVisible(key: ListFieldKey): boolean {
    return this.visibleFieldKeys.includes(key);
  }

  onToggleField(key: ListFieldKey, checked: boolean): void {
    if (key === 'name') {
      return;
    }

    if (checked && !this.visibleFieldKeys.includes(key)) {
      this.visibleFieldKeys = [...this.visibleFieldKeys, key];
      return;
    }

    if (!checked) {
      this.visibleFieldKeys = this.visibleFieldKeys.filter((field) => field !== key);
    }
  }

  onSaveDetails(): void {
    if (this.detailsForm.invalid) {
      this.detailsForm.markAllAsTouched();
      return;
    }

    const value = this.detailsForm.getRawValue();
    this.saveDetails.emit({
      name: value.name,
      description: value.description,
      entityType: value.entityType || undefined,
      visibility: value.visibility,
    });
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
}
