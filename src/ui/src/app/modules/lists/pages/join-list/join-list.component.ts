import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PageTemplateComponent } from '../../../../components/page-template/page-template.component';
import { MaterialModule } from '../../../../core/material.module';
import { ListService } from '../../services/list.service';

@Component({
  selector: 'app-join-list',
  imports: [ReactiveFormsModule, RouterLink, MaterialModule, PageTemplateComponent],
  templateUrl: './join-list.component.html',
  styleUrl: './join-list.component.scss',
})
export class JoinListComponent implements OnInit {
  private readonly formBuilder = inject(FormBuilder);
  private readonly listService = inject(ListService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly form = this.formBuilder.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(20)]],
  });

  busy = false;
  error = '';

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    if (code) {
      this.form.patchValue({ code });
    }
  }

  onSubmit(): void {
    if (this.form.invalid || this.busy) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy = true;
    this.error = '';
    const code = this.form.controls.code.value.trim();

    this.listService.redeemInvite({ code }).subscribe({
      next: (result) => {
        void this.router.navigate(['/lists', result.listId]);
      },
      error: (err) => {
        this.busy = false;
        this.error = err?.error?.message || 'Unable to join with that code.';
      },
    });
  }
}
