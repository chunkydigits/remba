import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { MaterialModule } from '../../core/material.module';
import { PageTemplateModule } from '../../components/page-template/page-template.module';
import { HomeComponent } from './pages/home/home.component';

const routes: Routes = [
  {
    path: '',
    component: HomeComponent,
  },
];

@NgModule({
  declarations: [HomeComponent],
  imports: [CommonModule, MaterialModule, PageTemplateModule, RouterModule.forChild(routes)],
})
export class HomeModule {}
