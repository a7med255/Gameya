import { routes } from './app.routes';

describe('role-specific routes', () => {
  it('does not grant PaymentUser access to the owner tree', () => {
    const owner = routes.find((route) => route.path === 'owner');
    expect(owner?.data?.['roles']).toEqual(['Admin', 'Owner']);
  });

  it('keeps the participant in a separate one-page route tree', () => {
    const participant = routes.find((route) => route.path === 'participant');
    expect(participant?.data?.['roles']).toEqual(['Participant']);
    expect(participant?.children?.length).toBe(1);
  });
});
