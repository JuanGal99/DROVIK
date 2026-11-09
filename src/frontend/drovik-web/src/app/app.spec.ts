import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { signal } from '@angular/core';
import { AuthService } from './core/auth/auth.service';
import { routes } from './app.routes';
import { App } from './app';

describe('App', () => {
  const fakeAuth = {
    isAuthenticated: true,
    ready: signal(true),
    busy: signal(false),
    error: signal<string | null>(null),
    user: signal({ id: 'test', email: 'demo@example.com', roles: ['Administrator'] }),
    initialize: () => of(undefined),
    logout: () => of(undefined),
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes), provideHttpClient(), { provide: AuthService, useValue: fakeAuth }],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should show the logistics navigation and synthetic operation state', async () => {
    const fixture = TestBed.createComponent(App);
    await TestBed.inject(Router).navigateByUrl('/overview');
    await fixture.whenStable();
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Resumen');
    expect(compiled.textContent).toContain('Northline Logistics');
  });

  it('collapses the sidebar without removing navigation access', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app.sidebarCollapsed()).toBe(false);
    app.toggleSidebar();
    fixture.detectChanges();
    expect(app.sidebarCollapsed()).toBe(true);
    expect(fixture.nativeElement.querySelector('.sidebar-collapsed')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.sidebar')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[title="Flota"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.nav-count')).toBeNull();
    expect(fixture.nativeElement.querySelector('.sidebar .sidebar-toggle')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.topbar .sidebar-toggle')).toBeNull();
  });

  it('removes sidebar text content from the collapsed layout', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    app.toggleSidebar();
    fixture.detectChanges();

    const sidebar = fixture.nativeElement.querySelector('.sidebar') as HTMLElement;
    expect(sidebar.querySelector('.brand-name')?.hasAttribute('hidden')).toBe(true);
    expect(sidebar.querySelector('.profile-copy')?.hasAttribute('hidden')).toBe(true);
    expect(sidebar.querySelector('.nav-item span')?.hasAttribute('hidden')).toBe(true);
    expect(sidebar.querySelector('[title="Resumen"]')).not.toBeNull();
    expect(sidebar.querySelector('[aria-label="Cerrar sesión"]')).not.toBeNull();
  });
});
