import { Component } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { IconsService } from './_shared/services/icons.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  constructor(private iconsService: IconsService,
    private router: Router
  ) {
    this.iconsService.registerIcons();
  }

  public home() {
    this.router.navigate(['/']);
  }
}