import { FormBuilder, FormControl, FormGroup, Validators } from '@angular/forms';
import { ListAttributeDefinition, ListAttributeType } from '../models/list.model';

export type EntryAttributesFormGroup = FormGroup<Record<string, FormControl<string | boolean>>>;

export function normaliseAttributeType(type: string | null | undefined): ListAttributeType {
  const value = (type || 'text').trim().toLowerCase();
  if (value === 'truefalse' || value === 'true-false' || value === 'true/false' || value === 'boolean') {
    return 'boolean';
  }
  if (value === 'number') {
    return 'number';
  }
  if (value === 'url') {
    return 'url';
  }
  if (value === 'list' || value === 'chips' || value === 'tags' || value === 'csv' || value === 'comma-separated') {
    return 'list';
  }
  return 'text';
}

export function createAttributesFormGroup(
  formBuilder: FormBuilder,
  definitions: ListAttributeDefinition[],
  values?: Record<string, unknown> | null,
): EntryAttributesFormGroup {
  const controls: Record<string, FormControl<string | boolean>> = {};

  for (const definition of definitions) {
    const key = definition.key?.trim();
    if (!key) {
      continue;
    }

    const type = normaliseAttributeType(definition.type);
    const raw = values?.[key];

    if (type === 'boolean') {
      controls[key] = formBuilder.nonNullable.control(coerceBoolean(raw));
      continue;
    }

    if (type === 'number') {
      const numberValue = coerceNumber(raw);
      controls[key] = formBuilder.nonNullable.control(
        numberValue === null ? '' : String(numberValue),
        { validators: [Validators.maxLength(40)] },
      );
      continue;
    }

    if (type === 'url') {
      controls[key] = formBuilder.nonNullable.control(coerceString(raw), {
        validators: [Validators.maxLength(2000)],
      });
      continue;
    }

    if (type === 'list') {
      controls[key] = formBuilder.nonNullable.control(normaliseListInput(raw), {
        validators: [Validators.maxLength(2000)],
      });
      continue;
    }

    controls[key] = formBuilder.nonNullable.control(coerceString(raw), {
      validators: [Validators.maxLength(1000)],
    });
  }

  return formBuilder.group(controls);
}

export function syncAttributesFormGroup(
  formGroup: FormGroup,
  formBuilder: FormBuilder,
  definitions: ListAttributeDefinition[],
  values?: Record<string, unknown> | null,
): void {
  const next = createAttributesFormGroup(formBuilder, definitions, values);
  for (const key of Object.keys(formGroup.controls)) {
    if (!(key in next.controls)) {
      formGroup.removeControl(key);
    }
  }

  for (const [key, control] of Object.entries(next.controls)) {
    if (formGroup.contains(key)) {
      formGroup.get(key)!.setValue(control.value, { emitEvent: false });
    } else {
      formGroup.addControl(key, control);
    }
  }
}

export function serializeAttributes(
  definitions: ListAttributeDefinition[],
  values: Record<string, unknown>,
): Record<string, unknown> {
  const result: Record<string, unknown> = {};

  for (const definition of definitions) {
    const key = definition.key?.trim();
    if (!key) {
      continue;
    }

    const type = normaliseAttributeType(definition.type);
    const raw = values[key];

    if (type === 'boolean') {
      result[key] = coerceBoolean(raw);
      continue;
    }

    if (type === 'number') {
      const numberValue = coerceNumber(raw);
      if (numberValue === null) {
        continue;
      }
      result[key] = numberValue;
      continue;
    }

    if (type === 'list') {
      const items = parseListItems(raw);
      if (items.length === 0) {
        continue;
      }
      result[key] = items.join(', ');
      continue;
    }

    const text = coerceString(raw).trim();
    if (!text) {
      continue;
    }
    result[key] = text;
  }

  return result;
}

export function formatAttributeValue(
  definition: ListAttributeDefinition,
  values: Record<string, unknown> | null | undefined,
  options?: { includeFalseBoolean?: boolean },
): string {
  const key = definition.key?.trim();
  if (!key || !values) {
    return '';
  }

  const type = normaliseAttributeType(definition.type);
  const raw = values[key];

  if (type === 'boolean') {
    if (raw === undefined || raw === null || raw === '') {
      return '';
    }
    if (coerceBoolean(raw)) {
      return 'Yes';
    }
    return options?.includeFalseBoolean ? 'No' : '';
  }

  if (type === 'number') {
    const numberValue = coerceNumber(raw);
    return numberValue === null ? '' : String(numberValue);
  }

  if (type === 'list') {
    return parseListItems(raw).join(', ');
  }

  return coerceString(raw).trim();
}

export function parseListItems(value: unknown): string[] {
  if (Array.isArray(value)) {
    return value
      .map((item) => coerceString(item).trim())
      .filter(Boolean);
  }

  const text = coerceString(value).trim();
  if (!text) {
    return [];
  }

  return text
    .split(',')
    .map((part) => part.trim())
    .filter(Boolean);
}

export function findAttributeDefinition(
  definitions: ListAttributeDefinition[] | null | undefined,
  key: string,
): ListAttributeDefinition | undefined {
  const normalised = key.trim().toLowerCase();
  return (definitions || []).find((definition) => definition.key?.trim().toLowerCase() === normalised);
}

function normaliseListInput(value: unknown): string {
  return parseListItems(value).join(', ');
}

function coerceString(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }
  return String(value);
}

function coerceBoolean(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return value;
  }
  if (typeof value === 'number') {
    return value !== 0;
  }
  const text = coerceString(value).trim().toLowerCase();
  return text === 'true' || text === 'yes' || text === '1';
}

function coerceNumber(value: unknown): number | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }
  const parsed = Number(String(value).trim());
  return Number.isFinite(parsed) ? parsed : null;
}
