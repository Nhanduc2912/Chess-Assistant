import { useState, useEffect } from 'react';
import * as signalR from '@microsoft/signalr';

export const useSignalR = (url) => {
  const [connection, setConnection] = useState(null);
  const [isOnline, setIsOnline] = useState(false);
  const [latestAnalysis, setLatestAnalysis] = useState(null);

  useEffect(() => {
    const newConnection = new signalR.HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect([1000, 2000, 4000, 8000, 15000, 30000]) // Exponential backoff for dropped connections
      .configureLogging(signalR.LogLevel.Information)
      .build();

    setConnection(newConnection);
  }, [url]);

  useEffect(() => {
    let isMounted = true;

    if (connection) {
      const startConnection = async () => {
        try {
          await connection.start();
          if (isMounted) {
            setIsOnline(true);
            console.log('Connected to SignalR Hub');
            
            connection.on('ReceiveAnalysis', (result) => {
              console.log('Analysis received:', result);
              setLatestAnalysis(result);
            });
          }
        } catch (e) {
          console.error('Connection failed, retrying in 5s...', e);
          if (isMounted) {
            setIsOnline(false);
            setTimeout(startConnection, 5000);
          }
        }
      };

      startConnection();

      connection.onreconnecting(() => setIsOnline(false));
      connection.onreconnected(() => setIsOnline(true));
      connection.onclose(() => setIsOnline(false));
    }

    return () => {
      isMounted = false;
      if (connection) {
        connection.stop();
      }
    };
  }, [connection]);

  return { isOnline, latestAnalysis };
};
