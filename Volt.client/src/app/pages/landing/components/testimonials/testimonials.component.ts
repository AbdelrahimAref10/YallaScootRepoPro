import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { LandingTranslatePipe } from '../../pipes/landing-translate.pipe';
import { RevealDirective } from '../../shared/reveal.directive';
import { PhotoComponent } from '../../shared/photo.component';

@Component({
  selector: 'app-testimonials',
  standalone: true,
  imports: [CommonModule, LandingTranslatePipe, RevealDirective, PhotoComponent],
  templateUrl: './testimonials.component.html'
})
export class TestimonialsComponent implements OnInit, OnDestroy {
  stars = [1, 2, 3, 4, 5];
  notes = [
    { quote: 'TESTIMONIALS.Q1', name: 'TESTIMONIALS.N1', file: 'rider-1.jpg' },
    { quote: 'TESTIMONIALS.Q2', name: 'TESTIMONIALS.N2', file: 'rider-2.jpg' },
    { quote: 'TESTIMONIALS.Q3', name: 'TESTIMONIALS.N3', file: 'rider-3.jpg' },
    { quote: 'TESTIMONIALS.Q4', name: 'TESTIMONIALS.N4', file: 'rider-4.jpg' }
  ];
  index = 0;
  perView = 3;
  animating = true;
  paused = false;
  private timer = 0;
  private reduced = false;

  get slides() {
    return [...this.notes, ...this.notes];
  }

  get shift() {
    return (this.index * 100) / this.perView;
  }

  ngOnInit() {
    this.reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    this.animating = !this.reduced;
    this.updatePerView();
    this.start();
  }

  ngOnDestroy() {
    this.stop();
  }

  @HostListener('window:resize')
  updatePerView() {
    const width = window.innerWidth;
    this.perView = width < 768 ? 1 : width < 1024 ? 2 : 3;
  }

  next() {
    this.animating = !this.reduced;
    this.index += 1;
    if (this.index >= this.notes.length) {
      window.setTimeout(() => this.jump(0), 700);
    }
  }

  prev() {
    if (this.index === 0) {
      this.animating = false;
      this.index = this.notes.length;
      requestAnimationFrame(() => {
        requestAnimationFrame(() => {
          this.animating = !this.reduced;
          this.index = this.notes.length - 1;
        });
      });
      return;
    }
    this.animating = !this.reduced;
    this.index -= 1;
  }

  go(dot: number) {
    this.animating = !this.reduced;
    this.index = dot;
    this.restart();
  }

  pause() {
    this.paused = true;
    this.stop();
  }

  resume() {
    this.paused = false;
    this.start();
  }

  step(direction: number) {
    if (direction < 0) this.prev();
    else this.next();
    this.restart();
  }

  private jump(value: number) {
    this.animating = false;
    this.index = value;
    requestAnimationFrame(() => {
      this.animating = !this.reduced;
    });
  }

  private start() {
    this.stop();
    if (this.reduced || this.paused) return;
    this.timer = window.setInterval(() => this.next(), 4500);
  }

  private restart() {
    if (!this.paused) this.start();
  }

  private stop() {
    if (this.timer) window.clearInterval(this.timer);
    this.timer = 0;
  }
}
