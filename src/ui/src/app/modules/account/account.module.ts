import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { RouterModule, Routes } from '@angular/router';
import { MaterialModule } from '../../core/material.module';
import { PageTemplateModule } from '../../components/page-template/page-template.module';
import { AccountComponent } from './pages/account/account.component';

const routes: Routes = [
  {
    path: '',
    component: AccountComponent,
  },
];

@NgModule({
  declarations: [AccountComponent],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MaterialModule,
    PageTemplateModule,
    RouterModule.forChild(routes),
  ],
})
export class AccountModule {}
