import { Injectable } from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';

@Injectable({
  providedIn: 'root'
})
export class IconsService {
  private icons: { name: string; path: string }[] = [
    { name: 'arrow_selector_tool', path: 'icons/arrow_selector_tool.svg' },
    { name: 'database', path: 'icons/database.svg' }
  ];

  constructor(private iconRegistry: MatIconRegistry, private sanitizer: DomSanitizer) {}

  public registerIcons(): void {
    this.icons.forEach(icon => {
      this.iconRegistry.addSvgIcon(
        icon.name,
        this.sanitizer.bypassSecurityTrustResourceUrl(icon.path)
      );
    });
  }
}