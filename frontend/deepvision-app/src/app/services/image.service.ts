import { Injectable } from '@angular/core';
import { HttpClient, HttpEventType, HttpRequest, HttpResponse } from '@angular/common/http';
import { Observable, map, filter } from 'rxjs';
import { ProcessImageResponse } from '../models/image-result.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ImageService {
  private readonly apiUrl = `${environment.apiBaseUrl}/image`;

  constructor(private http: HttpClient) {}

  processImage(file: File): Observable<ProcessImageResponse> {
    const formData = new FormData();
    formData.append('image', file, file.name);

    return this.http
      .post<ProcessImageResponse>(`${this.apiUrl}/process`, formData)
      .pipe(map((response) => response));
  }

  buildImageSrc(base64: string, mimeType: string): string {
    return `data:${mimeType};base64,${base64}`;
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
  }
}
