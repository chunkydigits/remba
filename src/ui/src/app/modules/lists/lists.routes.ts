import { Routes } from '@angular/router';
import { MyListsComponent } from './pages/my-lists/my-lists.component';
import { CreateListComponent } from './pages/create-list/create-list.component';
import { ListDetailComponent } from './pages/list-detail/list-detail.component';
import { AddEntryComponent } from './pages/add-entry/add-entry.component';

export const LISTS_ROUTES: Routes = [
  {
    path: '',
    component: MyListsComponent,
  },
  {
    path: 'new',
    component: CreateListComponent,
  },
  {
    path: ':listId/add',
    component: AddEntryComponent,
  },
  {
    path: ':listId',
    component: ListDetailComponent,
  },
];
