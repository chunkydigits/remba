import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { Component, Input } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatChipInputEvent } from '@angular/material/chips';
import { MaterialModule } from '../../../../core/material.module';
import { ListAttributeDefinition } from '../../models/list.model';
import { normaliseAttributeType, parseListItems } from '../../utils/entry-attributes.util';

@Component({
  selector: 'app-entry-attributes-fields',
  imports: [ReactiveFormsModule, MaterialModule],
  templateUrl: './entry-attributes-fields.component.html',
  styleUrl: './entry-attributes-fields.component.scss',
})
export class EntryAttributesFieldsComponent {
  @Input({ required: true }) definitions: ListAttributeDefinition[] = [];
  @Input({ required: true }) formGroup!: FormGroup;
  @Input() heading = 'Custom properties';

  readonly normaliseType = normaliseAttributeType;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;

  get hasFields(): boolean {
    return this.definitions.some((definition) => !!definition.key?.trim());
  }

  listItems(key: string): string[] {
    return parseListItems(this.formGroup.get(key)?.value);
  }

  addListItem(event: MatChipInputEvent, key: string): void {
    const value = (event.value || '').trim();
    event.chipInput?.clear();
    if (!value) {
      return;
    }

    const items = this.listItems(key);
    if (items.some((item) => item.toLowerCase() === value.toLowerCase())) {
      return;
    }

    items.push(value);
    this.setListItems(key, items);
  }

  removeListItem(key: string, item: string): void {
    this.setListItems(
      key,
      this.listItems(key).filter((existing) => existing !== item),
    );
  }

  private setListItems(key: string, items: string[]): void {
    this.formGroup.get(key)?.setValue(items.join(', '));
    this.formGroup.get(key)?.markAsDirty();
  }
}
