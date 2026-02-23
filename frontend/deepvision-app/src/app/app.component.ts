import { Component } from '@angular/core';
import { ImageProcessorComponent } from './components/image-processor/image-processor.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [ImageProcessorComponent],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent {}
