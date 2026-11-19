import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule, Routes } from '@angular/router';
import { MaterialModule } from '../../core/material.module';
import { PageTemplateModule } from '../../components/page-template/page-template.module';
import { MyListsComponent } from './pages/my-lists/my-lists.component';
import { CreateListComponent } from './pages/create-list/create-list.component';
import { ListDetailComponent } from './pages/list-detail/list-detail.component';

const routes: Routes = [
  {
    path: '',
    component: MyListsComponent,
  },
  {
    path: 'new',
    component: CreateListComponent,
  },
  {
    path: ':listId',
    component: ListDetailComponent,
  },
];

@NgModule({
  declarations: [MyListsComponent, CreateListComponent, ListDetailComponent],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MaterialModule,
    PageTemplateModule,
    RouterModule.forChild(routes),
  ],
})
export class ListsModule {}
