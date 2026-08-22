/** Entry route: redirects to tabs or login once the session is restored. */
import { Redirect } from 'expo-router';
import { useAuth } from '../lib/auth';

export default function Index() {
  const { status } = useAuth();
  if (status === 'restoring') return null;
  return <Redirect href={status === 'signedIn' ? '/(tabs)' : '/(auth)/login'} />;
}
