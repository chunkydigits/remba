import { Routes } from '@angular/router';
import { MyListsComponent } from './pages/my-lists/my-lists.component';
import { CreateListComponent } from './pages/create-list/create-list.component';
import { JoinListComponent } from './pages/join-list/join-list.component';
import { ListDetailComponent } from './pages/list-detail/list-detail.component';
import { ListSettingsPageComponent } from './pages/list-settings-page/list-settings-page.component';
import { AddEntryComponent } from './pages/add-entry/add-entry.component';
import { EditEntryComponent } from './pages/edit-entry/edit-entry.component';

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
    path: 'join',
    component: JoinListComponent,
  },
  {
    path: ':listId/add',
    component: AddEntryComponent,
  },
  {
    path: ':listId/settings',
    component: ListSettingsPageComponent,
  },
  {
    path: ':listId/entries/:entryId',
    component: EditEntryComponent,
  },
  {
    path: ':listId',
    component: ListDetailComponent,
  },
];
