import { OrderState } from '../../core/services/clientAPI';

export interface PipelineStep {
  state: OrderState;
  key: string;
}

/** The stages an order moves through, in order (shown as the stepper on order pages). */
export const ORDER_PIPELINE_STEPS: readonly PipelineStep[] = [
  { state: OrderState.Pending, key: 'common.pending' },
  { state: OrderState.MerchantPending, key: 'common.merchantPending' },
  { state: OrderState.MerchantConfirmed, key: 'common.merchantConfirmed' },
  { state: OrderState.Confirmed, key: 'common.confirmed' },
  { state: OrderState.DeliveryAssigned, key: 'common.deliveryAssigned' },
  { state: OrderState.OnWay, key: 'common.onWay' },
  { state: OrderState.CustomerReceived, key: 'common.received' },
  { state: OrderState.Completed, key: 'common.completed' }
];

/** Whether an order in `current` state has passed, reached or not yet reached `step`. */
export function pipelineStepStatus(current: OrderState | null | undefined, step: OrderState): 'done' | 'active' | 'upcoming' {
  if (current == null) return 'upcoming';
  if (current > step) return 'done';
  if (current === step) return 'active';
  return 'upcoming';
}

/** How far along the pipeline the order is, 0–100, for the stepper's fill line. */
export function pipelineProgress(current: OrderState | null | undefined): number {
  if (current == null) return 0;
  const steps = ORDER_PIPELINE_STEPS;
  const index = steps.findIndex(step => step.state === current);
  const reached = index >= 0 ? index : steps.filter(step => step.state < current).length - 1;
  return Math.max(0, Math.min(100, (reached / (steps.length - 1)) * 100));
}
