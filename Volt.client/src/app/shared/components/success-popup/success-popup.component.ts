import { ChangeDetectionStrategy, Component, EventEmitter, Input, OnDestroy, OnInit, Output } from '@angular/core';
import { PortalDirective } from '../../directives/portal.directive';

/**
 * Celebratory confirmation shown after a successful action. It closes itself after
 * `duration` ms (a bar shows the countdown) and emits `done`; clicking it finishes early.
 *
 *   @if (created) {
 *     <app-success-popup [title]="..." [message]="..." [badge]="order.orderCode" (done)="openOrder()" />
 *   }
 */
@Component({
  selector: 'app-success-popup',
  standalone: true,
  imports: [PortalDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './success-popup.component.html',
  styleUrl: './success-popup.component.css'
})
export class SuccessPopupComponent implements OnInit, OnDestroy {
  @Input({ required: true }) title = '';
  @Input() message = '';
  /** Short highlighted value, e.g. the new order code. */
  @Input() badge = '';
  @Input() actionLabel = '';
  @Input() duration = 2200;
  @Output() done = new EventEmitter<void>();

  private timer?: ReturnType<typeof setTimeout>;
  private finished = false;

  ngOnInit(): void {
    this.timer = setTimeout(() => this.finish(), this.duration);
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }

  finish(): void {
    if (this.finished) return;
    this.finished = true;
    clearTimeout(this.timer);
    this.done.emit();
  }
}
