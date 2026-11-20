import { AsyncPipe } from '@angular/common';
import { Component, forwardRef, inject, OnInit } from '@angular/core';
import {
  ControlValueAccessor,
  FormBuilder,
  NG_VALUE_ACCESSOR,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { BehaviorSubject, catchError, of, tap } from 'rxjs';
import { MaterialModule } from '../../../../core/material.module';
import { EntityTypeService } from '../../services/entity-type.service';
import { EntityType } from '../../models/entity-type.model';

const ADD_NEW_VALUE = '__add_new__';

@Component({
  selector: 'app-entity-type-select',
  imports: [AsyncPipe, ReactiveFormsModule, MaterialModule],
  templateUrl: './entity-type-select.component.html',
  styleUrl: './entity-type-select.component.scss',
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EntityTypeSelectComponent),
      multi: true,
    },
  ],
})
export class EntityTypeSelectComponent implements ControlValueAccessor, OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly entityTypeService = inject(EntityTypeService);
  private readonly typesSubject = new BehaviorSubject<EntityType[]>([]);

  readonly types$ = this.typesSubject.asObservable();
  readonly addNewValue = ADD_NEW_VALUE;

  addingNew = false;
  createError = '';
  disabled = false;
  selectedKey = '';

  readonly newTypeForm = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
  });

  private onChange: (value: string) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  ngOnInit(): void {
    this.reloadTypes();
  }

  writeValue(value: string | null): void {
    this.selectedKey = value || '';
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  onSelectChange(value: string): void {
    if (value === ADD_NEW_VALUE) {
      this.addingNew = true;
      this.createError = '';
      this.newTypeForm.reset({ name: '' });
      return;
    }

    this.addingNew = false;
    this.selectedKey = value;
    this.onChange(value);
    this.markTouched();
  }

  markTouched(): void {
    this.onTouched();
  }

  onCancelAdd(): void {
    this.addingNew = false;
    this.createError = '';
    this.newTypeForm.reset({ name: '' });
  }

  onCreateType(): void {
    if (this.newTypeForm.invalid) {
      this.newTypeForm.markAllAsTouched();
      return;
    }

    this.createError = '';
    const name = this.newTypeForm.controls.name.value;

    this.entityTypeService
      .create({ name })
      .pipe(
        tap((created) => {
          const next = [...this.typesSubject.value, created].sort((a, b) =>
            a.name.localeCompare(b.name),
          );
          this.typesSubject.next(next);
          this.addingNew = false;
          this.selectedKey = created.key;
          this.onChange(created.key);
          this.markTouched();
          this.newTypeForm.reset({ name: '' });
        }),
        catchError((error) => {
          this.createError = error?.error?.message || 'Unable to create entity type.';
          return of(null);
        }),
      )
      .subscribe();
  }

  private reloadTypes(): void {
    this.entityTypeService
      .getAvailable()
      .pipe(catchError(() => of([] as EntityType[])))
      .subscribe((types) => {
        this.typesSubject.next(types);

        // Keep a previously saved custom/system value selectable even if reload races.
        if (this.selectedKey && !types.some((type) => type.key === this.selectedKey)) {
          this.typesSubject.next([
            ...types,
            {
              key: this.selectedKey,
              name: this.selectedKey,
              visibility: 'private',
              supportsLookup: false,
              added: new Date().toISOString(),
              edited: null,
            },
          ]);
        }
      });
  }
}
