import React, { useState, useEffect } from 'react';

import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

interface LocalState {
    device: any;
    isLoading: boolean;
    errorMessage?: string;
}

export const Home = () => {
    const [state, setState] = useState<LocalState>();
    const [hubConnection, setHubConnection] = useState<HubConnection>();

    useEffect(
        () => {
            const createHubConnection = async () => {
                const connection = new HubConnectionBuilder()
                    .withUrl('/hub/device')
                    .configureLogging(LogLevel.Debug)
                    .withAutomaticReconnect()
                    .build();

                connection.on('DeviceChanged', (deviceData: any) => {
                    console.log('deviceChanged:', deviceData);
                    setState((oldState) => ({ ...oldState, device: deviceData } as LocalState));
                });

                connection.onreconnecting(() => {
                    console.warn('hub connection lost');
                    setHubConnection(undefined);
                });

                connection.onreconnected(async () => {
                    console.warn('hub connection recovered');
                    setHubConnection(connection);
                });

                await connection.start();
                setHubConnection(connection);

                const device = await connection.invoke('GetDevice');
                console.log('device is', device);
                setState((oldState) => ({ ...oldState, device: device } as LocalState));
            };

            createHubConnection();
        },
        [] /* Effect will run only once */,
    );

    const sendHubCommand = (methodName: string, ...args: unknown[]) => {
        console.log(hubConnection);
        hubConnection?.send(methodName, ...args);
    };

    return (
        <div>
            <div className="row mt-5">
                <div className="col"></div>
                <div className="col-lg-6 col-md-8 col-sm-12 text-center">
                    <h3>{state?.device.templateName} Module</h3>
                    <h2 className="my-4">{state?.device.templateName}</h2>
                    <h4>
                        {state?.device.status.statusNet.hostName} ({state?.device.status.statusNet.ipAddress})
                    </h4>
                    <h1 className="display-2 my-5">{state?.device.status.statusSts.power}</h1>
                    <button
                        type="button"
                        className="btn btn-primary btn-lg btn-block mb-4"
                        onClick={() => sendHubCommand('TogglePower', '1')}
                    >
                        Toggle
                    </button>
                    <button type="button" className="btn btn-danger btn-lg btn-block mb-4">
                        Restart
                    </button>
                </div>
                <div className="col"></div>
            </div>
            <div className="row">
                <div className="col"></div>
                <div className="col-lg-6 col-md-8 col-sm-12 text-right border-top">
                    <small className="text-muted">Tasmota emulator by F.D.Castel</small>
                </div>
                <div className="col"></div>
            </div>
        </div>
    );
};

Home.displayName = Home.name;
