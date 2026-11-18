import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Routes } from '@angular/router';
import { MaterialModule } from '../../core/material.module';
import { PageTemplateModule } from '../../components/page-template/page-template.module';
import { SettingsComponent } from './pages/settings/settings.component';

const routes: Routes = [
  {
    path: '',
    component: SettingsComponent,
  },
];

@NgModule({
  declarations: [SettingsComponent],
  imports: [CommonModule, MaterialModule, PageTemplateModule, RouterModule.forChild(routes)],
})
export class SettingsModule {}
