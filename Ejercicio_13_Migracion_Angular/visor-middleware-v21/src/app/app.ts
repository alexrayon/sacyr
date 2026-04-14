import { Component } from '@angular/core';
import { MiddlewareV21Component } from './middleware-v21.component';

@Component({
  selector: 'app-root',
  imports: [MiddlewareV21Component],
  template: `
    <app-middleware-status-v21></app-middleware-status-v21>
  `,
  styleUrl: './app.css'
})
export class App {}
