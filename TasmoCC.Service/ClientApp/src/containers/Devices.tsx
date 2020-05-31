import React, { useState, useReducer, useEffect, useCallback, Dispatch, ReactNode } from 'react';
import moment from 'moment';
import Moment from 'react-moment';

import Alert from 'react-bootstrap/Alert';
import Badge from 'react-bootstrap/Badge';
import Button from 'react-bootstrap/Button';
import Collapse from 'react-bootstrap/Collapse';
import Form from 'react-bootstrap/Form';
import Col from 'react-bootstrap/Col';
import Row from 'react-bootstrap/Row';
import Spinner from 'react-bootstrap/Spinner';
import Tab from 'react-bootstrap/Tab';
import Tabs from 'react-bootstrap/Tabs';

import semverLt from 'semver/functions/lt';

import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
    faSatelliteDish,
    faChild,
    faTags,
    faGlobeAmericas,
    faNetworkWired,
    faMicrochip,
    faWrench,
    faHammer,
} from '@fortawesome/free-solid-svg-icons';

import './Devices.scss';

import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

type DeviceState =
    | 'AdoptionPending'
    | 'Adopting'
    | 'ProvisionPending'
    | 'Provisioning'
    | 'Connected'
    | 'Restarting'
    | 'Upgrading';

interface DeviceStatus {
    uptimeSeconds: number;
    heapKb: number;
    cpuLoad: number;

    mqttRetries: number;

    wiFiRssi: number;
    wiFiDbm: number;
    wiFiRetries: number;
    wiFiDowntimeSeconds: number;

    powerStates: string[];
}

interface DeviceConfiguration {
    disabled?: boolean;
    friendlyNames: string[];
    setupCommands: string;
    templateName: string;
    topicName: string;
}

interface Template {
    _id: string;

    definition: string;
    thumbnailUrl: string;
    imageUrl: string;
}

interface Device {
    _id: string;

    hostName: string;

    ipv4Address: string;
    ipv4SubnetPrefix: string;
    ipv4Gateway: string;
    ipv4NameServer: string;

    topicName: string;
    friendlyNames: string[];

    firmwareSizeKb: number;
    flashSizeKb: number;
    firmwareVersion: string;
    hardware: string;

    restartReason: string;
    telemetrySeconds: number;

    templateDefinition: string;
    templateName: string;

    status: DeviceStatus;
    offline: boolean;

    state: DeviceState;

    adoptedAt?: Date;
    provisionedAt?: Date;
    updatedAt: Date;

    configuration?: DeviceConfiguration;
    template?: Template;
}

const REQUIRED_FIRMWARE_VERSION = '8.2.0';

const isFirmwareVersionUnsupported = (device?: Device) =>
    device ? semverLt(device.firmwareVersion, REQUIRED_FIRMWARE_VERSION) : undefined;

interface FirmwareVersionProps {
    device?: Device;
}

const FirmwareVersion = ({ device }: FirmwareVersionProps) => {
    if (!device) {
        return null;
    }

    const isUnsupported = isFirmwareVersionUnsupported(device);

    return (
        <div
            className={isUnsupported ? 'text-danger' : ''}
            title={
                isUnsupported
                    ? `TasmoCC requires Tasmota ${REQUIRED_FIRMWARE_VERSION} or later. Please upgrade.`
                    : 'Firmware version'
            }
        >
            {device.firmwareVersion}
        </div>
    );
};

interface StateBadgeProps {
    device?: Device;
    pill?: boolean;
}

const StateBadge = ({ device, pill }: StateBadgeProps) => {
    if (!device) {
        return null;
    }

    type BadgeVariant = 'primary' | 'secondary' | 'warning' | 'danger' | 'success';

    function getStateBadgeInfo(device: Device): { variant: BadgeVariant; caption: string } {
        if (device.offline) {
            return { variant: 'danger', caption: 'Offline' };
        }
        if (device.state === 'Restarting') {
            return { variant: 'secondary', caption: 'Restarting' };
        }
        if (device.state === 'Upgrading') {
            return { variant: 'secondary', caption: 'Upgrading' };
        }
        if (isFirmwareVersionUnsupported(device)) {
            return { variant: 'danger', caption: 'Unsupported' };
        }
        if (!device.adoptedAt && device.state !== 'Adopting') {
            return { variant: 'warning', caption: 'Pending adoption' };
        }
        if (!device.adoptedAt && device.state === 'Adopting') {
            return { variant: 'warning', caption: 'Adopting' };
        }
        if (device.adoptedAt && device.state !== 'Provisioning' && !device.provisionedAt) {
            return { variant: 'success', caption: 'Pending provision' };
        }
        if (device.adoptedAt && device.state === 'Provisioning' && !device.provisionedAt) {
            return { variant: 'success', caption: 'Provisioning' };
        }
        return { variant: 'primary', caption: 'Connected' };
    }

    const badgeInfo = getStateBadgeInfo(device);

    return (
        <Badge variant={badgeInfo.variant} pill={pill} title="State">
            {badgeInfo.caption}
        </Badge>
    );
};

interface AdoptButtonProps {
    device: Device;
    hubConnection?: HubConnection;
}

const AdoptButton = ({ device, hubConnection }: AdoptButtonProps) => (
    <Button
        variant="warning"
        disabled={!hubConnection}
        hidden={!!device.adoptedAt || device.state === 'Adopting' || isFirmwareVersionUnsupported(device)}
        onClick={() => hubConnection?.send('Adopt', device._id)}
    >
        <span className="mr-1">
            <FontAwesomeIcon icon={faChild} fixedWidth />
        </span>
        Adopt
    </Button>
);

interface PowerSwitchesProps {
    device: Device;
    hubConnection?: HubConnection;
}

const PowerSwitches = ({ device, hubConnection }: PowerSwitchesProps) => {
    const friendlyNames: string[] = device.friendlyNames;
    const powerStates: string[] = device.status.powerStates;

    // Keeps ESLint happy
    const deviceId = device._id;
    const deviceOffline = device.offline;
    const deviceAdoptedAt = device.adoptedAt;

    const renderPowerSwitch = (friendlyName: string, powerState: string, index: number) => {
        const toggleClicked = () => hubConnection?.send('SetPower', deviceId, index + 1, 'TOGGLE');
        return deviceAdoptedAt ? (
            <Form.Check
                custom
                type="switch"
                key={index}
                id={`switch-${deviceId}-${index}`}
                label={friendlyName}
                disabled={!hubConnection || deviceOffline}
                checked={powerState === 'ON'}
                tabIndex={index}
                onChange={toggleClicked}
            />
        ) : (
            <span key={index}>
                <Button
                    variant="secondary"
                    size="sm"
                    className="mr-2 px-1 py-0"
                    title="Adopt this device to get its state feedback"
                    tabIndex={index}
                    onClick={toggleClicked}
                >
                    Toggle
                </Button>
                {friendlyName}
            </span>
        );
    };

    return <Form>{friendlyNames.map((fn, i) => renderPowerSwitch(fn, powerStates[i], i))}</Form>;
};

interface SidebarDetailsProps {
    device?: Device;
}

const SidebarDetails = ({ device }: SidebarDetailsProps) => (
    <div className="border-left border-right border-bottom border-secondary p-2">
        <h4 className="text-muted">
            <span className="mr-2">
                <FontAwesomeIcon icon={faGlobeAmericas} fixedWidth />
            </span>
            Overview
        </h4>
        <table className="table table-sm table-borderless">
            <tbody>
                <tr>
                    <td className="text-muted">Topic name</td>
                    <td>{device?.topicName}</td>
                </tr>
                <tr>
                    <td className="text-muted">Template</td>
                    <td>{device?.templateName}</td>
                </tr>
                <tr>
                    <td className="text-muted">Hardware</td>
                    <td>{device?.hardware}</td>
                </tr>
                <tr>
                    <td className="text-muted">Restart reason</td>
                    <td>{device?.restartReason}</td>
                </tr>
                <tr>
                    <td className="text-muted">Telemetry interval</td>
                    <td>{moment.duration(device?.telemetrySeconds, 'seconds').humanize({ ss: 1 })}</td>
                </tr>
                <tr>
                    <td className="text-muted">Last activity</td>
                    <td>
                        <Moment durationFromNow date={device?.updatedAt}></Moment>
                    </td>
                </tr>
                <tr>
                    <td className="text-muted">Uptime</td>
                    <td>
                        <Moment
                            durationFromNow
                            date={device?.updatedAt}
                            subtract={{ seconds: device?.status?.uptimeSeconds }}
                        ></Moment>
                    </td>
                </tr>

                <tr>
                    <td className="text-muted">Heap</td>
                    <td>{device?.status?.heapKb} KB</td>
                </tr>
                <tr>
                    <td className="text-muted">CPU load</td>
                    <td>{device?.status?.cpuLoad}%</td>
                </tr>
                <tr>
                    <td className="text-muted">MQTT retries</td>
                    <td>{device?.status?.mqttRetries}</td>
                </tr>
            </tbody>
        </table>

        <h4 className="text-muted">
            <span className="mr-2">
                <FontAwesomeIcon icon={faNetworkWired} fixedWidth />
            </span>
            Network
        </h4>
        <table className="table table-sm table-borderless">
            <tbody>
                <tr>
                    <td className="text-muted">MAC address</td>
                    <td>{device?._id}</td>
                </tr>
                <tr>
                    <td className="text-muted">Hostname</td>
                    <td>{device?.hostName}</td>
                </tr>
                <tr>
                    <td className="text-muted">IPv4 address</td>
                    <td>
                        <a href={'http://' + device?.ipv4Address} target="_blank" rel="noreferrer noopener">
                            {device?.ipv4Address}
                        </a>
                        /{device?.ipv4SubnetPrefix}
                    </td>
                </tr>
                <tr>
                    <td className="text-muted">IPv4 gateway</td>
                    <td>{device?.ipv4Gateway}</td>
                </tr>
                <tr>
                    <td className="text-muted">IPv4 name server</td>
                    <td>{device?.ipv4Gateway}</td>
                </tr>
                <tr>
                    <td className="text-muted">Wi-Fi signal</td>
                    <td>
                        {device?.status?.wiFiRssi}% / {device?.status?.wiFiDbm}dBm
                    </td>
                </tr>
                <tr>
                    <td className="text-muted">Wi-Fi retries</td>
                    <td>{device?.status?.wiFiRetries}</td>
                </tr>
                <tr>
                    <td className="text-muted">Wi-Fi downtime</td>
                    <td>{device?.status?.wiFiDowntimeSeconds} seconds</td>
                </tr>
            </tbody>
        </table>

        <h4 className="text-muted">
            <span className="mr-2">
                <FontAwesomeIcon icon={faMicrochip} fixedWidth />
            </span>
            Firmware
        </h4>
        <table className="table table-sm table-borderless">
            <tbody>
                <tr>
                    <td className="text-muted">Version</td>
                    <td>
                        <FirmwareVersion device={device} />
                    </td>
                </tr>
                <tr>
                    <td className="text-muted">Size</td>
                    <td>{device?.firmwareSizeKb} KB</td>
                </tr>
                <tr>
                    <td className="text-muted">Total storage</td>
                    <td>{device?.flashSizeKb} KB</td>
                </tr>
            </tbody>
        </table>
    </div>
);

interface SidebarConfigurationProps {
    device?: Device;
    templates: Template[];
    hubConnection?: HubConnection;
}

const SidebarConfiguration = ({ device, templates, hubConnection }: SidebarConfigurationProps) => {
    const completeConfiguration = (device?: Device) =>
        device
            ? ({
                  friendlyNames: device.configuration?.friendlyNames ?? device.friendlyNames,
                  setupCommands: device.configuration?.setupCommands ?? '',
                  templateName: device.configuration?.templateName ?? device.templateName,
                  topicName: device.configuration?.topicName ?? device.topicName,
              } as DeviceConfiguration)
            : ({
                  friendlyNames: [],
                  setupCommands: '',
                  templateName: '',
                  topicName: '',
              } as DeviceConfiguration);

    const [isSubmitting, setIsSubmitting] = useState(false);
    const [newConfiguration, setNewConfiguration] = useState(completeConfiguration(device));

    // Reset state when device changes
    useEffect(() => {
        setIsSubmitting(false);
        setNewConfiguration(completeConfiguration(device));
    }, [device]);

    const resetClicked = () => {
        setIsSubmitting(false);
        setNewConfiguration(completeConfiguration(device));
    };

    const submitClicked = () => {
        setIsSubmitting(true);
        hubConnection?.send('SetConfiguration', device?._id, newConfiguration);
    };

    const templateNameChanged = (event: React.ChangeEvent<HTMLInputElement>) =>
        setNewConfiguration({ ...newConfiguration, templateName: event.target.value });

    const topicNameChanged = (event: React.ChangeEvent<HTMLInputElement>) =>
        setNewConfiguration({ ...newConfiguration, topicName: event.target.value });

    const friendlyNameChanged = (event: React.ChangeEvent<HTMLInputElement>) => {
        const index = event.target.tabIndex;
        const newNames = newConfiguration.friendlyNames.map((v, i) => (i === index ? event.target.value : v));
        setNewConfiguration({ ...newConfiguration, friendlyNames: newNames });
    };

    const setupCommandsChanged = (event: React.ChangeEvent<HTMLInputElement>) =>
        setNewConfiguration({ ...newConfiguration, setupCommands: event.target.value });

    const hasChanges = JSON.stringify(newConfiguration) !== JSON.stringify(completeConfiguration(device));

    return (
        <div className="border-left border-right border-bottom border-secondary p-2">
            <Form onSubmit={submitClicked}>
                <h4 className="text-muted">
                    <span className="mr-2">
                        <FontAwesomeIcon icon={faWrench} fixedWidth />
                    </span>
                    General
                </h4>

                <Form.Group className="mb-4">
                    <Form.Label className="text-muted">Template</Form.Label>
                    <Form.Control
                        as="select"
                        custom
                        value={newConfiguration.templateName}
                        onChange={templateNameChanged}
                    >
                        {templates.map((t: Template) => (
                            <option key={t._id}>{t._id}</option>
                        ))}
                    </Form.Control>
                    <small className="form-text text-muted">
                        Search for 1000+ devices in{' '}
                        <a href="https://templates.blakadder.com/" target="_blank" rel="noreferrer noopener">
                            Tasmota Device Templates Repository
                        </a>
                    </small>
                </Form.Group>

                <Form.Group className="mb-4">
                    <Form.Label className="text-muted">Topic name</Form.Label>
                    <Form.Control
                        type="text"
                        placeholder="Topic name"
                        value={newConfiguration.topicName}
                        onChange={topicNameChanged}
                    />
                    <small className="form-text text-muted">MQTT topic name for this device.</small>
                </Form.Group>

                <Form.Label className="text-muted">Friendly names</Form.Label>
                <div className="mb-4">
                    {newConfiguration.friendlyNames.map((name: string, index: number) => (
                        <Form.Group as={Row} key={index}>
                            <Form.Label column sm={2} className="text-muted">
                                Relay {index + 1}
                            </Form.Label>
                            <Col>
                                <Form.Control
                                    type="text"
                                    id={`config-friendly-${index}`}
                                    tabIndex={index}
                                    placeholder={`Friendly name for relay ${index + 1}`}
                                    value={newConfiguration.friendlyNames[index]}
                                    onChange={friendlyNameChanged}
                                />
                            </Col>
                        </Form.Group>
                    ))}
                </div>

                <Form.Group className="mb-4">
                    <Form.Label className="text-muted">Additional commands</Form.Label>
                    <Form.Control
                        type="text"
                        placeholder="Enter commands separated by a semicolon (;)"
                        value={newConfiguration.setupCommands}
                        onChange={setupCommandsChanged}
                    />
                    <small className="form-text text-muted">
                        You can inform here{' '}
                        <a href="https://tasmota.github.io/docs/Commands/" target="_blank" rel="noreferrer noopener">
                            custom commands
                        </a>{' '}
                        used during device provision.
                    </small>
                </Form.Group>

                <Collapse in={hasChanges}>
                    <div className="mt-4">
                        <div className="d-flex justify-content-between">
                            <Button variant="secondary" onClick={resetClicked}>
                                Discard changes
                            </Button>

                            <Button variant="primary" onClick={submitClicked} disabled={isSubmitting}>
                                {isSubmitting ? (
                                    <span>
                                        Applying... <Spinner animation="border" size="sm" as="span" />
                                    </span>
                                ) : (
                                    <span>Apply changes</span>
                                )}
                            </Button>
                        </div>
                    </div>
                </Collapse>
            </Form>
        </div>
    );
};

interface SidebarManageProps {
    device?: Device;
    hubConnection?: HubConnection;
}

const SidebarManage = ({ device, hubConnection }: SidebarManageProps) => {
    const [keepWiFi, setKeepWiFi] = useState(true);

    const keepWifiChanged = (event: React.ChangeEvent<HTMLInputElement>) => setKeepWiFi(event.target.checked);

    // Reset state when device changes
    useEffect(() => {
        setKeepWiFi(true);
    }, [device]);

    return (
        <div className="border-left border-right border-bottom border-secondary p-2">
            <h4 className="text-muted">
                <span className="mr-2">
                    <FontAwesomeIcon icon={faHammer} fixedWidth />
                </span>
                Management
            </h4>
            <h5>Restart</h5>
            <div className="d-flex justify-content-between align-items-start mb-4">
                <div className="text-muted">Restart this device. It should be unavailable for a few seconds.</div>
                <Button
                    variant="primary"
                    disabled={device?.state === 'Restarting' || !hubConnection}
                    onClick={() => hubConnection?.send('Restart', device?._id)}
                >
                    Restart
                </Button>
            </div>
            <h5>Firmware upgrade</h5>
            <div className="d-flex justify-content-between align-items-start mb-4">
                <div className="text-muted">Upgrade this device firmware to the latest release.</div>
                <Button
                    variant="primary"
                    disabled={device?.state === 'Upgrading' || !hubConnection}
                    onClick={() => hubConnection?.send('Upgrade', device?._id)}
                >
                    Upgrade
                </Button>
            </div>
            <h5>Force provision</h5>
            <div className="d-flex justify-content-between align-items-start mb-4">
                <div className="text-muted">Ensure that the device has all the latest settings applied to it.</div>
                <Button
                    variant="primary"
                    disabled={device?.state === 'Provisioning' || !hubConnection}
                    onClick={() => hubConnection?.send('Provision', device?._id)}
                >
                    Provision
                </Button>
            </div>
            <h5>
                Disable <small className="text-secondary">(coming soon)</small>
            </h5>
            <div className="d-flex justify-content-between align-items-start mb-4">
                <div className="text-muted">A disabled device will be hidden from the device list.</div>
                <Button variant="primary" disabled={true} onClick={() => hubConnection?.send('Disable', device?._id)}>
                    Disable
                </Button>
            </div>
            <h5>Forget</h5>
            <div className="d-flex justify-content-between align-items-start mb-4">
                <div className="text-muted">
                    Remove all configuration and history of this device. Also reset its MQTT settings to firmware
                    defaults.
                </div>
                <Button
                    variant="primary"
                    disabled={!device?.adoptedAt || !hubConnection}
                    onClick={() => hubConnection?.send('Forget', device?._id)}
                >
                    Forget
                </Button>
            </div>
            <h5>
                Download device information <small className="text-secondary">(coming soon)</small>
            </h5>
            <div className="d-flex justify-content-between align-items-start mb-4">
                <div className="text-muted">Download all known information for this device in a JSON file.</div>
                <Button variant="primary" disabled={true}>
                    Download
                </Button>
            </div>
            <h5 className="text-danger">Reset to firmware defaults</h5>
            <div className="d-flex justify-content-between align-items-start mb-4">
                <div className="text-muted">
                    <div>Reset device settings to firmware defaults and restart.</div>

                    <Form.Check
                        custom
                        type="checkbox"
                        id="config-keepWiFi"
                        label="Keep Wi-Fi settings"
                        checked={keepWiFi}
                        onChange={keepWifiChanged}
                    />
                </div>
                <Button
                    variant="danger"
                    disabled={!hubConnection}
                    onClick={() => hubConnection?.send('ResetConfiguration', device?._id, keepWiFi)}
                >
                    Reset
                </Button>
            </div>

            <Alert variant="warning" show={!keepWiFi}>
                Resetting the device Wi-Fi settings would probably <u>make it unresponsive</u> in this network. Use this
                option with care.
            </Alert>
        </div>
    );
};

interface LocalStateAction {
    type:
        | 'CONNECTION_CHANGED'
        | 'INITIAL_PAYLOAD_RECEIVED'
        | 'DEVICE_CHANGED'
        | 'SIDEBAR_DEVICE_CHANGED'
        | 'NETWORK_SCAN_CHANGED';
    payload: unknown;
}

interface SidebarProps {
    device?: Device;
    templates: Template[];
    dispatchState: Dispatch<LocalStateAction>;
    hubConnection?: HubConnection;
    children: ReactNode;
}

const Sidebar = ({ device, templates, dispatchState, hubConnection, children }: SidebarProps) => (
    <div className="row">
        <div className={device ? '' : 'sidebar-hidden'}>
            <div className="sidebar border-left rounded-left border-top border-bottom border-secondary bg-dark p-2">
                <div className="d-flex justify-content-between align-items-start border-bottom border-secondary p-0">
                    <h4>
                        <span className="mr-2">
                            <FontAwesomeIcon icon={faTags} fixedWidth />
                        </span>
                        {device?.topicName}
                    </h4>
                    <button
                        type="button"
                        className="close"
                        onClick={() => dispatchState({ type: 'SIDEBAR_DEVICE_CHANGED', payload: undefined })}
                    >
                        &times;
                    </button>
                </div>
                <div className="mt-2">
                    <div className="sidebar-header d-flex justify-content-between mt-2">
                        <h4>
                            <StateBadge device={device} />
                        </h4>
                        <img src={device?.template?.thumbnailUrl} alt="" title={device?.template?._id}></img>
                    </div>
                    <Tabs defaultActiveKey="details" id="sidebarTabs">
                        <Tab eventKey="details" title="Details">
                            <SidebarDetails device={device} />
                        </Tab>

                        <Tab eventKey="configuration" title="Configuration">
                            <SidebarConfiguration device={device} templates={templates} hubConnection={hubConnection} />
                        </Tab>

                        <Tab eventKey="manage" title="Manage">
                            <SidebarManage device={device} hubConnection={hubConnection} />
                        </Tab>
                    </Tabs>
                </div>
            </div>
        </div>
        <div className="col">{children}</div>
        <div className="col-sidebar-reserved-area d-none d-xl-block"></div>
    </div>
);

interface DeviceListProps {
    devices: Device[];
    selectedDevice?: Device;
    dispatchState: Dispatch<LocalStateAction>;
    hubConnection?: HubConnection;
}

const DeviceList = ({ devices, selectedDevice, dispatchState, hubConnection }: DeviceListProps) => {
    const renderNoDevices = () => (
        <tr>
            <td colSpan={9} className="text-center text-muted">
                <p>There are no devices registered.</p>
                <p>Use the &quot;Scan Network&quot; button above to search for new devices.</p>
            </td>
        </tr>
    );

    const renderDevice = (device: Device) => (
        <tr
            key={device._id}
            className={(device.offline ? 'text-secondary' : '') + (device === selectedDevice ? ' bg-dark' : '')}
            onClick={() => dispatchState({ type: 'SIDEBAR_DEVICE_CHANGED', payload: device })}
        >
            <td className="thumbnail-col text-center">
                <img
                    src={device?.template?.thumbnailUrl}
                    className={'thumbnail ' + (device.offline ? 'thumbnail-disabled' : '')}
                    alt=""
                    title={device?.template?._id}
                ></img>
            </td>
            <td>
                <div title="Topic name">{device.topicName}</div>
                <StateBadge device={device} />
            </td>
            <td>
                <div>
                    <a
                        href={'http://' + device.ipv4Address}
                        target="_blank"
                        rel="noreferrer noopener"
                        title="IPv4 address"
                        hidden={device.offline}
                    >
                        {device.ipv4Address}
                    </a>
                    <span title="IPv4 address" hidden={!device.offline}>
                        {device.ipv4Address}
                    </span>
                </div>
                <small className={device.offline ? 'text-secondary' : 'text-muted'} title="MAC address">
                    {device._id}
                </small>
            </td>
            <td>
                <FirmwareVersion device={device} />
                <small className={device.offline ? 'text-secondary' : 'text-muted'} title="Hardware">
                    {device.hardware}
                </small>
            </td>
            <td className="text-center" hidden={device.offline}>
                <h5 className="mb-0" title="Signal strength (RSSI)">
                    {device.status.wiFiRssi}%
                </h5>
                <small className="text-muted" title="Signal strength (dBm)">
                    {device.status.wiFiDbm}dBm
                </small>
            </td>
            <td className="text-center" title="Average CPU load" hidden={device.offline}>
                <h5 className="mb-0">{device.status.cpuLoad}%</h5>
                <small className="text-muted" title="Heap memory available">
                    {device.status.heapKb} KB
                </small>
            </td>
            <td hidden={device.offline}>
                <div>
                    <Moment
                        durationFromNow
                        date={device.updatedAt}
                        subtract={{ seconds: device.status.uptimeSeconds }}
                    ></Moment>
                </div>
                <small className="text-muted" title="Restart reason">
                    {device.restartReason}
                </small>
            </td>
            <td colSpan={3} hidden={!device.offline}></td>
            <td className="align-middle">
                <PowerSwitches device={device} hubConnection={hubConnection} />
            </td>
            <td className="align-middle">
                <AdoptButton device={device} hubConnection={hubConnection} />
            </td>
        </tr>
    );

    return (
        <table className="table table-sm table-hover">
            <caption>Showing {devices.length} devices</caption>
            <thead>
                <tr>
                    <th></th>
                    <th>Device</th>
                    <th>Address</th>
                    <th>Version</th>
                    <th className="text-center">Wi-Fi</th>
                    <th className="text-center">Load</th>
                    <th>Uptime</th>
                    <th>Power</th>
                    <th></th>
                </tr>
            </thead>
            <tbody>{devices.length === 0 ? renderNoDevices() : devices.map((d) => renderDevice(d))}</tbody>
        </table>
    );
};

interface LocalState {
    hubConnection?: HubConnection;
    devices: Device[];
    templates: Template[];
    sidebarDevice?: Device;
    isLoading: boolean;
    isScanning: boolean;
}

type ChangeKind = 'Insert' | 'Update' | 'Replace' | 'Delete';

interface InitialPayloadReceived {
    devices: Device[];
    templates: Template[];
}

interface DeviceChanged {
    device: Device;
    changeKind: ChangeKind;
}

export const Devices = () => {
    function stateReducer(state: LocalState, action: LocalStateAction): LocalState {
        switch (action.type) {
            case 'CONNECTION_CHANGED':
                const hubConnection = action.payload as HubConnection | undefined;
                return {
                    ...state,
                    hubConnection: hubConnection,
                };

            case 'INITIAL_PAYLOAD_RECEIVED':
                const { devices, templates } = action.payload as InitialPayloadReceived;
                return {
                    ...state,
                    isLoading: false,
                    devices: devices.sort((a, b) => a.ipv4Address?.localeCompare(b.ipv4Address)),
                    templates: templates.sort((a, b) => a._id?.localeCompare(b._id)),
                };

            case 'DEVICE_CHANGED':
                const { device, changeKind } = action.payload as DeviceChanged;

                const newDevices = state.devices.slice();
                const i = newDevices.findIndex((el: Device) => el._id === device._id);
                let newSidebarDevice: Device | undefined;
                if (changeKind === 'Delete') {
                    newDevices.splice(i, 1);
                    newSidebarDevice = state.sidebarDevice?._id === device._id ? undefined : state.sidebarDevice;
                } else {
                    if (i === -1) {
                        newDevices.push(device);
                    } else {
                        newDevices[i] = device;
                    }
                    newSidebarDevice = state.sidebarDevice?._id === device._id ? device : state.sidebarDevice;
                }

                return {
                    ...state,
                    devices: newDevices,
                    sidebarDevice: newSidebarDevice,
                };

            case 'SIDEBAR_DEVICE_CHANGED':
                const selectedDevice = action.payload as Device | undefined;
                return {
                    ...state,
                    sidebarDevice: selectedDevice,
                };

            case 'NETWORK_SCAN_CHANGED':
                const isScanning = action.payload as boolean;
                return {
                    ...state,
                    isScanning: isScanning,
                };

            default:
                throw new Error('Unexpected action type.');
        }
    }

    const initialState: LocalState = {
        hubConnection: undefined,
        devices: [],
        templates: [],
        sidebarDevice: undefined,
        isLoading: true,
        isScanning: false,
    };

    const [state, dispatchState] = useReducer(stateReducer, initialState);

    const createHubConnection = useCallback(async () => {
        const connection = new HubConnectionBuilder()
            .withUrl('/hub/devices')
            .configureLogging(LogLevel.Warning)
            .withAutomaticReconnect()
            .build();

        connection.on('InitialPayloadReceived', (devices: Device[], templates: Template[]) => {
            dispatchState({ type: 'INITIAL_PAYLOAD_RECEIVED', payload: { devices, templates } });
        });

        connection.on('DeviceChanged', (device: Device, changeKind: ChangeKind) => {
            dispatchState({ type: 'DEVICE_CHANGED', payload: { device, changeKind } });
        });

        connection.on('NetworkScanFinished', () => {
            dispatchState({ type: 'NETWORK_SCAN_CHANGED', payload: false });
        });

        connection.onreconnecting(() => {
            dispatchState({ type: 'CONNECTION_CHANGED', payload: undefined });
        });

        connection.onclose(() => {
            dispatchState({ type: 'CONNECTION_CHANGED', payload: undefined });
        });

        connection.onreconnected(() => {
            dispatchState({ type: 'CONNECTION_CHANGED', payload: connection });
        });

        await connection.start();
        dispatchState({ type: 'CONNECTION_CHANGED', payload: connection });
    }, []);

    // Effect will run only once.
    useEffect(() => {
        createHubConnection();
    }, [createHubConnection]);

    const scanNetworkClicked = () => {
        dispatchState({ type: 'NETWORK_SCAN_CHANGED', payload: true });
        state.hubConnection?.send('ScanNetwork');
    };

    return (
        <div>
            <Sidebar
                device={state.sidebarDevice}
                templates={state.templates}
                dispatchState={dispatchState}
                hubConnection={state.hubConnection}
            >
                <div className="d-flex justify-content-between">
                    <h2>Your devices</h2>
                    <button
                        type="button"
                        className="btn btn-secondary align-self-center"
                        disabled={!state.hubConnection || state.isScanning}
                        onClick={scanNetworkClicked}
                    >
                        <span className="mr-1">
                            <FontAwesomeIcon icon={faSatelliteDish} fixedWidth />
                        </span>

                        {state.isScanning ? (
                            <span>
                                Scanning... <Spinner animation="border" size="sm" as="span" />
                            </span>
                        ) : (
                            <span>Scan network</span>
                        )}
                    </button>
                </div>
                {state.isLoading ? (
                    <div className="text-muted">
                        <Spinner animation="border" />
                    </div>
                ) : (
                    <DeviceList
                        devices={state.devices}
                        selectedDevice={state.sidebarDevice}
                        dispatchState={dispatchState}
                        hubConnection={state.hubConnection}
                    />
                )}
            </Sidebar>
        </div>
    );
};

Devices.displayName = Devices.name;
