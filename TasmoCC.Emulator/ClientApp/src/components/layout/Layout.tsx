import React from 'react';

export const Layout = (props: any) => (
    <div>
        <div className="container">{props.children}</div>
    </div>
);

Layout.displayName = Layout.name;
