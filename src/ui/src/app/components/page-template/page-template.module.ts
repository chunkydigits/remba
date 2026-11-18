import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MaterialModule } from '../../core/material.module';
import { PageTemplateComponent } from './page-template.component';

@NgModule({
  declarations: [PageTemplateComponent],
  imports: [CommonModule, RouterModule, MaterialModule],
  exports: [PageTemplateComponent],
})
export class PageTemplateModule {}
