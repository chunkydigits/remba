import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  BehaviorSubject,
  Observable,
  Subject,
  catchError,
  combineLatest,
  filter,
  map,
  of,
  shareReplay,
  switchMap,
  tap,
} from 'rxjs';
import { ConfirmDialogComponent } from '../../../../components/confirm-dialog/confirm-dialog.component';
import { PageTemplateComponent } from '../../../../components/page-template/page-template.component';
import { MaterialModule } from '../../../../core/material.module';
import { ListSettingsComponent } from '../../components/list-settings/list-settings.component';
import { ListService } from '../../services/list.service';
import {
  ListDetail,
  UpdateListRequest,
  UpdateListViewSettingsRequest,
} from '../../models/list.model';

@Component({
  selector: 'app-list-settings-page',
  imports: [
    AsyncPipe,
    RouterLink,
    MaterialModule,
    PageTemplateComponent,
    ListSettingsComponent,
  ],
  templateUrl: './list-settings-page.component.html',
  styleUrl: './list-settings-page.component.scss',
})
export class ListSettingsPageComponent {
  private readonly listService = inject(ListService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly listReload$ = new BehaviorSubject<void>(undefined);
  private readonly saveDetailsRequests = new Subject<UpdateListRequest>();
  private readonly saveViewSettingsRequests = new Subject<UpdateListViewSettingsRequest>();
  private readonly deleteRequests = new Subject<void>();

  saveError = '';
  saveSuccess = false;
  deleteError = '';

  readonly listId$: Observable<string | null> = this.route.paramMap.pipe(
    map((params) => params.get('listId')),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly list$: Observable<ListDetail | null> = combineLatest([this.listId$, this.listReload$]).pipe(
    switchMap(([listId]) => {
      if (!listId) {
        return of(null);
      }

      return this.listService.getOwnList(listId).pipe(
        catchError(() => of(null)),
      );
    }),
    shareReplay({ bufferSize: 1, refCount: true }),
  );

  readonly saveDetailsResult$: Observable<ListDetail | null> = this.saveDetailsRequests.pipe(
    switchMap((request) => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId) {
        return of(null);
      }

      this.saveError = '';
      this.saveSuccess = false;
      return this.listService.update(listId, request).pipe(
        tap(() => {
          this.saveSuccess = true;
          this.listReload$.next();
        }),
        catchError((error) => {
          this.saveError = error?.error?.message || 'Unable to update list.';
          return of(null);
        }),
      );
    }),
  );

  readonly saveViewSettingsResult$: Observable<ListDetail | null> = this.saveViewSettingsRequests.pipe(
    switchMap((request) => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId) {
        return of(null);
      }

      this.saveError = '';
      this.saveSuccess = false;
      return this.listService.updateViewSettings(listId, request).pipe(
        tap(() => {
          this.saveSuccess = true;
          this.listReload$.next();
        }),
        catchError((error) => {
          this.saveError = error?.error?.message || 'Unable to update display settings.';
          return of(null);
        }),
      );
    }),
  );

  readonly deleteResult$: Observable<boolean | null> = this.deleteRequests.pipe(
    switchMap(() => {
      const listId = this.route.snapshot.paramMap.get('listId');
      if (!listId) {
        return of(null);
      }

      this.deleteError = '';
      return this.listService.delete(listId).pipe(
        tap(() => {
          void this.router.navigate(['/lists']);
        }),
        map(() => true),
        catchError((error) => {
          this.deleteError = error?.error?.message || 'Unable to delete list.';
          return of(false);
        }),
      );
    }),
  );

  onSaveDetails(request: UpdateListRequest): void {
    this.saveDetailsRequests.next(request);
  }

  onSaveViewSettings(request: UpdateListViewSettingsRequest): void {
    this.saveViewSettingsRequests.next(request);
  }

  onDeleteList(): void {
    this.deleteRequests.next();
  }

  onSharesChanged(): void {
    this.listReload$.next();
  }

  onLeaveList(): void {
    const listId = this.route.snapshot.paramMap.get('listId');
    if (!listId) {
      return;
    }

    this.dialog
      .open(ConfirmDialogComponent, {
        width: '22rem',
        data: {
          title: 'Leave shared list?',
          message: 'You will lose access until someone shares it with you again.',
          confirmLabel: 'Leave',
          cancelLabel: 'Stay',
        },
      })
      .afterClosed()
      .pipe(filter((confirmed): confirmed is true => confirmed === true))
      .subscribe(() => {
        this.listService.leave(listId).subscribe({
          next: () => {
            void this.router.navigate(['/lists']);
          },
          error: (error) => {
            this.saveError = error?.error?.message || 'Unable to leave list.';
          },
        });
      });
  }
}
