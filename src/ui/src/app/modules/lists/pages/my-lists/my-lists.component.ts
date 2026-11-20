import { AsyncPipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Observable, catchError, of, startWith, switchMap } from 'rxjs';
import { Subject } from 'rxjs';
import { PageTemplateComponent } from '../../../../components/page-template/page-template.component';
import { MaterialModule } from '../../../../core/material.module';
import { ListService } from '../../services/list.service';
import { ListSummary } from '../../models/list.model';

@Component({
  selector: 'app-my-lists',
  imports: [AsyncPipe, RouterLink, MaterialModule, PageTemplateComponent],
  templateUrl: './my-lists.component.html',
  styleUrl: './my-lists.component.scss',
})
export class MyListsComponent {
  private readonly listService = inject(ListService);
  private readonly refresh = new Subject<void>();

  readonly lists$: Observable<ListSummary[] | null> = this.refresh.pipe(
    startWith(undefined),
    switchMap(() =>
      this.listService.getOwnLists().pipe(
        catchError(() => of([] as ListSummary[])),
      ),
    ),
  );
}
