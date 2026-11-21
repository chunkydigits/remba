import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable, Subject, catchError, of, shareReplay, switchMap, tap } from 'rxjs';
import { PageTemplateComponent } from '../../../../components/page-template/page-template.component';
import { MaterialModule } from '../../../../core/material.module';
import { EntityTypeSelectComponent } from '../../components/entity-type-select/entity-type-select.component';
import { ListService } from '../../services/list.service';
import { SuggestedListService } from '../../services/suggested-list.service';
import { ListDetail } from '../../models/list.model';
import { SuggestedListDetail } from '../../models/suggested-list.model';

@Component({
  selector: 'app-create-list',
  imports: [
    AsyncPipe,
    ReactiveFormsModule,
    RouterLink,
    MaterialModule,
    PageTemplateComponent,
    EntityTypeSelectComponent,
  ],
  templateUrl: './create-list.component.html',
  styleUrl: './create-list.component.scss',
})
export class CreateListComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly listService = inject(ListService);
  private readonly suggestedListService = inject(SuggestedListService);
  private readonly router = inject(Router);
  private readonly saveRequests = new Subject<void>();

  saveError = '';
  selectedSeedKey: string | null = null;

  readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    description: ['', [Validators.maxLength(1000)]],
    entityType: ['', [Validators.maxLength(40)]],
    visibility: ['private', Validators.required],
  });

  readonly suggestedLists$: Observable<SuggestedListDetail[]> = this.suggestedListService.getAll().pipe(
    catchError(() => of([])),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly saveResult$: Observable<ListDetail | null> = this.saveRequests.pipe(
    switchMap(() => {
      if (this.form.invalid) {
        this.form.markAllAsTouched();
        return of(null);
      }

      this.saveError = '';
      const value = this.form.getRawValue();

      return this.listService
        .create({
          name: value.name,
          description: value.description || undefined,
          entityType: value.entityType || undefined,
          visibility: value.visibility,
        })
        .pipe(
          tap((list) => {
            void this.router.navigate(['/lists', list.id]);
          }),
          catchError((error) => {
            this.saveError = error?.error?.message || 'Unable to create list.';
            return of(null);
          }),
        );
    }),
  );

  onUseSuggested(template: SuggestedListDetail): void {
    this.selectedSeedKey = template.seedKey;
    this.form.patchValue({
      name: template.name,
      description: template.description || '',
      entityType: template.entityType || '',
    });
  }

  onSave(): void {
    this.saveRequests.next();
  }
}
